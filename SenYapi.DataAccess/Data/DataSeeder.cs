using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SenYapi.Core.Constants;
using SenYapi.Entity;

namespace SenYapi.DataAccess.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(SenYapiDbContext context, UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
        await context.Database.EnsureCreatedAsync();

        // Şema kontrolü ve güncellemesi (BlogComments, ContactMessages & ContactMessageReplies)
        try
        {
            if (context.Database.IsSqlServer())
            {
                await context.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BlogComments' AND COLUMN_NAME = 'ParentCommentId')
BEGIN
    ALTER TABLE BlogComments ADD ParentCommentId INT NULL;
    ALTER TABLE BlogComments ADD CONSTRAINT FK_BlogComments_BlogComments_ParentCommentId FOREIGN KEY (ParentCommentId) REFERENCES BlogComments(Id);
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BlogComments' AND COLUMN_NAME = 'UserRole')
BEGIN
    ALTER TABLE BlogComments ADD UserRole NVARCHAR(100) NULL;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BlogComments' AND COLUMN_NAME = 'IsStaffReply')
BEGIN
    ALTER TABLE BlogComments ADD IsStaffReply BIT NOT NULL DEFAULT 0;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessages' AND COLUMN_NAME = 'IsArchived')
BEGIN
    ALTER TABLE ContactMessages ADD IsArchived BIT NOT NULL DEFAULT 0;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessages' AND COLUMN_NAME = 'RepliedBy')
BEGIN
    ALTER TABLE ContactMessages ADD RepliedBy NVARCHAR(256) NULL;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessages' AND COLUMN_NAME = 'AccessToken')
BEGIN
    ALTER TABLE ContactMessages ADD AccessToken NVARCHAR(64) NOT NULL DEFAULT '';
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessages' AND COLUMN_NAME = 'UserId')
BEGIN
    ALTER TABLE ContactMessages ADD UserId NVARCHAR(450) NULL;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessages' AND COLUMN_NAME = 'UserHasUnreadReply')
BEGIN
    ALTER TABLE ContactMessages ADD UserHasUnreadReply BIT NOT NULL DEFAULT 0;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessages' AND COLUMN_NAME = 'AdminHasUnreadReply')
BEGIN
    ALTER TABLE ContactMessages ADD AdminHasUnreadReply BIT NOT NULL DEFAULT 0;
END;
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ContactMessageReplies')
BEGIN
    CREATE TABLE ContactMessageReplies (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ContactMessageId INT NOT NULL,
        ReplyText NVARCHAR(MAX) NOT NULL,
        RepliedBy NVARCHAR(256) NOT NULL,
        RepliedAt DATETIME2 NOT NULL,
        IsSuccess BIT NOT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_ContactMessageReplies_ContactMessages FOREIGN KEY (ContactMessageId) REFERENCES ContactMessages(Id) ON DELETE CASCADE
    );
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessageReplies' AND COLUMN_NAME = 'IsStaffReply')
BEGIN
    ALTER TABLE ContactMessageReplies ADD IsStaffReply BIT NOT NULL DEFAULT 1;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessageReplies' AND COLUMN_NAME = 'SenderName')
BEGIN
    ALTER TABLE ContactMessageReplies ADD SenderName NVARCHAR(256) NULL;
END;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ContactMessageReplies' AND COLUMN_NAME = 'UserId')
BEGIN
    ALTER TABLE ContactMessageReplies ADD UserId NVARCHAR(450) NULL;
END;");
            }
        }
        catch
        {
            // Veritabanı sağlayıcısı veya yetki durumunda devam et
        }

        // 1. Roles
        string[] roles = { "SystemAdmin", "Admin", "Editor", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole { Name = role });
            }
        }

        // 2. SystemAdmin User (Sistem Yöneticisi - Açıkça Belirlenen Sorumlu Hesap)
        var adminEmail = "admin@senyapi.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                UserName = "admin",
                Email = adminEmail,
                FullName = "Sistem Yöneticisi",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin123*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "SystemAdmin");
            }
        }
        else
        {
            var currentRoles = await userManager.GetRolesAsync(adminUser);
            if (!currentRoles.Contains("SystemAdmin"))
            {
                await userManager.AddToRoleAsync(adminUser, "SystemAdmin");
            }
            if (currentRoles.Contains("User"))
            {
                await userManager.RemoveFromRoleAsync(adminUser, "User");
            }
        }

        // 2.1 Normal Admin User (İçerik Yöneticisi - SystemAdmin Değildir)
        var normalAdminEmail = "admin2@senyapi.com";
        var normalAdminUser = await userManager.FindByEmailAsync(normalAdminEmail);
        if (normalAdminUser == null)
        {
            normalAdminUser = new AppUser
            {
                UserName = "admin2",
                Email = normalAdminEmail,
                FullName = "Normal Yönetici",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(normalAdminUser, "Admin123*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(normalAdminUser, "Admin");
            }
        }

        // Admin kullanıcısına başlangıç yetki claim'lerini ata
        if (normalAdminUser != null)
        {
            var claims = await userManager.GetClaimsAsync(normalAdminUser);
            if (!claims.Any(c => c.Type == "Permission"))
            {
                var adminPerms = new[]
                {
                    Permissions.AdminAccess,
                    Permissions.BlogView, Permissions.BlogCreate, Permissions.BlogEdit, Permissions.BlogPublish, Permissions.BlogDelete,
                    Permissions.ProjectView, Permissions.ProjectCreate, Permissions.ProjectEdit, Permissions.ProjectDelete,
                    Permissions.ContentView, Permissions.ContentCreate, Permissions.ContentEdit, Permissions.ContentDelete,
                    Permissions.MediaUpload, Permissions.MediaDelete,
                    Permissions.CommentView, Permissions.CommentReply, Permissions.CommentApprove, Permissions.CommentDelete,
                    Permissions.MessageView, Permissions.MessageReply, Permissions.MessageDelete,
                    Permissions.SettingsEdit
                };
                foreach (var p in adminPerms)
                {
                    await userManager.AddClaimAsync(normalAdminUser, new Claim("Permission", p));
                }
            }
        }

        // 2.2 Editor User
        var editorEmail = "editor@senyapi.com";
        var editorUser = await userManager.FindByEmailAsync(editorEmail);
        if (editorUser == null)
        {
            editorUser = new AppUser
            {
                UserName = "editor",
                Email = editorEmail,
                FullName = "İçerik Editörü",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(editorUser, "Editor123*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(editorUser, "Editor");
            }
        }

        // Editor kullanıcısına başlangıç yetki claim'lerini ata
        if (editorUser != null)
        {
            var claims = await userManager.GetClaimsAsync(editorUser);
            if (!claims.Any(c => c.Type == "Permission"))
            {
                var editorPerms = new[]
                {
                    Permissions.AdminAccess,
                    Permissions.BlogView, Permissions.BlogCreate, Permissions.BlogEdit, Permissions.BlogPublish,
                    Permissions.ProjectView, Permissions.ProjectCreate, Permissions.ProjectEdit,
                    Permissions.CommentView, Permissions.CommentReply
                };
                foreach (var p in editorPerms)
                {
                    await userManager.AddClaimAsync(editorUser, new Claim("Permission", p));
                }
            }
        }

        // 3. Regular Demo User
        var userEmail = "demo@senyapi.com";
        var normalUser = await userManager.FindByEmailAsync(userEmail);
        if (normalUser == null)
        {
            normalUser = new AppUser
            {
                UserName = "demo",
                Email = userEmail,
                FullName = "Demo Kullanıcı",
                EmailConfirmed = true,
                PhoneNumber = "05551234567",
                PhoneNumberConfirmed = true
            };
            var result = await userManager.CreateAsync(normalUser, "User123*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(normalUser, "User");
            }
        }
        else if (!normalUser.PhoneNumberConfirmed || string.IsNullOrEmpty(normalUser.PhoneNumber))
        {
            normalUser.PhoneNumber = "05551234567";
            normalUser.PhoneNumberConfirmed = true;
            await userManager.UpdateAsync(normalUser);
        }

        // 4. Services
        if (!await context.Services.AnyAsync())
        {
            context.Services.AddRange(
                new ServiceItem
                {
                    Title = "Bina İnşaatı",
                    Description = "Modern mühendislik ve mimari standartlarına uygun, sağlam ve güvenilir bina inşaat projeleri.",
                    ImageUrl = "~/theme/builderz/img/service-1.jpg"
                },
                new ServiceItem
                {
                    Title = "Ev ve Ofis Yenileme",
                    Description = "Yaşam ve çalışma alanlarınızı estetik, konforlu ve işlevsel şekilde baştan aşağı yeniliyoruz.",
                    ImageUrl = "~/theme/builderz/img/service-2.jpg"
                },
                new ServiceItem
                {
                    Title = "Mimari Tasarım",
                    Description = "Yaratıcı, fonksiyonel ve sürdürülebilir mimari konseptler ve 3D görselleştirme çözümleri.",
                    ImageUrl = "~/theme/builderz/img/service-3.jpg"
                },
                new ServiceItem
                {
                    Title = "İç Mimari & Dekorasyon",
                    Description = "Mekanlarınıza özel mobilya, aydınlatma ve zemin tasarımı ile estetik çözümler.",
                    ImageUrl = "~/theme/builderz/img/service-4.jpg"
                },
                new ServiceItem
                {
                    Title = "Dış Cephe & Boya",
                    Description = "Uzun ömürlü, ısı ve su yalıtımlı modern dış cephe kaplama ve boya uygulamaları.",
                    ImageUrl = "~/theme/builderz/img/service-5.jpg"
                },
                new ServiceItem
                {
                    Title = "Çatı & İzolasyon",
                    Description = "Her türlü hava koşuluna dayanıklı çatı tamiratı, su ve ısı izolasyon sistemleri.",
                    ImageUrl = "~/theme/builderz/img/service-6.jpg"
                }
            );
            await context.SaveChangesAsync();
        }

        // 5. Projects
        if (!await context.Projects.AnyAsync())
        {
            context.Projects.AddRange(
                new Project
                {
                    Title = "Merkez İş Kulesi",
                    Category = "complete",
                    CategoryName = "Tamamlanan",
                    ImageUrl = "~/theme/builderz/img/portfolio-1.jpg",
                    Description = "Modern mimariyle tasarlanmış, akıllı bina otomasyonuna sahip iş merkezi projesi."
                },
                new Project
                {
                    Title = "Yeşil Vadi Konutları",
                    Category = "running",
                    CategoryName = "Devam Eden",
                    ImageUrl = "~/theme/builderz/img/portfolio-2.jpg",
                    Description = "Doğayla iç içe, sürdürülebilir ve lüks aile yaşam alanları inşaatı."
                },
                new Project
                {
                    Title = "Boğaziçi Rezidans",
                    Category = "upcoming",
                    CategoryName = "Gelecek Proje",
                    ImageUrl = "~/theme/builderz/img/portfolio-3.jpg",
                    Description = "Panoramik manzaralı, ultra lüks rezidans ve sosyal donatı projesi."
                },
                new Project
                {
                    Title = "Liman Lojistik Merkezi",
                    Category = "complete",
                    CategoryName = "Tamamlanan",
                    ImageUrl = "~/theme/builderz/img/portfolio-4.jpg",
                    Description = "Yüksek kapasiteli depo ve dağıtım lojistik kompleksi inşası."
                },
                new Project
                {
                    Title = "Marina Alışveriş Vadisi",
                    Category = "running",
                    CategoryName = "Devam Eden",
                    ImageUrl = "~/theme/builderz/img/portfolio-5.jpg",
                    Description = "Açık hava konseptli alışveriş, restoran ve kültür merkezi."
                },
                new Project
                {
                    Title = "Güneş Enerjili Eko Kampüs",
                    Category = "upcoming",
                    CategoryName = "Gelecek Proje",
                    ImageUrl = "~/theme/builderz/img/portfolio-6.jpg",
                    Description = "Kendi enerjisini üreten çevre dostu yeni nesil eğitim kampüsü."
                }
            );
            await context.SaveChangesAsync();
        }

        // 6. Team Members
        if (!await context.TeamMembers.AnyAsync())
        {
            context.TeamMembers.AddRange(
                new TeamMember
                {
                    FullName = "Adam Phillips",
                    Designation = "CEO & Kurucu",
                    ImageUrl = "~/theme/builderz/img/team-1.jpg",
                    TwitterUrl = "https://twitter.com",
                    FacebookUrl = "https://facebook.com",
                    LinkedInUrl = "https://linkedin.com",
                    InstagramUrl = "https://instagram.com"
                },
                new TeamMember
                {
                    FullName = "Dylan Adams",
                    Designation = "İnşaat Baş Mühendisi",
                    ImageUrl = "~/theme/builderz/img/team-2.jpg",
                    TwitterUrl = "https://twitter.com",
                    FacebookUrl = "https://facebook.com",
                    LinkedInUrl = "https://linkedin.com",
                    InstagramUrl = "https://instagram.com"
                },
                new TeamMember
                {
                    FullName = "Jhon Doe",
                    Designation = "Baş Mimar",
                    ImageUrl = "~/theme/builderz/img/team-3.jpg",
                    TwitterUrl = "https://twitter.com",
                    FacebookUrl = "https://facebook.com",
                    LinkedInUrl = "https://linkedin.com",
                    InstagramUrl = "https://instagram.com"
                },
                new TeamMember
                {
                    FullName = "Josh Dunn",
                    Designation = "Şantiye Şefi",
                    ImageUrl = "~/theme/builderz/img/team-4.jpg",
                    TwitterUrl = "https://twitter.com",
                    FacebookUrl = "https://facebook.com",
                    LinkedInUrl = "https://linkedin.com",
                    InstagramUrl = "https://instagram.com"
                }
            );
            await context.SaveChangesAsync();
        }

        // 7. Testimonials
        if (!await context.Testimonials.AnyAsync())
        {
            context.Testimonials.AddRange(
                new Testimonial
                {
                    ClientName = "Michael Brown",
                    Profession = "İş Adamı",
                    Comment = "SenYapi ile çalışmak işimizin geleceğine yapılan en iyi yatırımdı. Proje teslimi tam zamanında ve kusursuz gerçekleşti.",
                    ImageUrl = "~/theme/builderz/img/testimonial-1.jpg"
                },
                new Testimonial
                {
                    ClientName = "Sarah Jenkins",
                    Profession = "Mimar & İç Mimar",
                    Comment = "Mimari detaylara gösterdikleri özen ve kaliteli malzeme kullanımı takdire şayan. Ekibin profesyonelliğine hayran kaldık.",
                    ImageUrl = "~/theme/builderz/img/testimonial-2.jpg"
                },
                new Testimonial
                {
                    ClientName = "David Wilson",
                    Profession = "Gayrimenkul Yatırımcısı",
                    Comment = "Büyük ölçekli şantiyelerde böylesine şeffaf ve güven veren bir yönetim kadrosu bulmak çok zor. Tebrikler!",
                    ImageUrl = "~/theme/builderz/img/testimonial-3.jpg"
                },
                new Testimonial
                {
                    ClientName = "Emma Watson",
                    Profession = "Tasarım Direktörü",
                    Comment = "Hayalimizdeki villayı beklentimizin çok ötesinde bir estetik ve sağlamlıkla inşa ettiler. Teşekkür ederiz.",
                    ImageUrl = "~/theme/builderz/img/testimonial-4.jpg"
                }
            );
            await context.SaveChangesAsync();
        }

        // 8. Blog Posts
        if (!await context.BlogPosts.AnyAsync())
        {
            var post1 = new BlogPost
            {
                Title = "Geleceğin Akıllı Binaları ve Sürdürülebilir Yapı Teknolojileri",
                Summary = "Enerji verimliliği ve çevre dostu malzemelerin geleceğin inşaat sektöründeki yeri ve etkileri.",
                Content = @"İnşaat sektörü hızla dönüşüyor. Günümüzde sadece sağlam yapılar inşa etmek yeterli değil; aynı zamanda karbon ayak izini minimize eden, kendi enerjisini üretebilen ve akıllı otomasyon sistemleriyle donatılmış binalar tasarlamak gerekiyor.

Bu makalemizde yeşil bina sertifikasyonları (LEED, BREEAM), güneş panelleriyle entegre cephe sistemleri ve yağmur suyu geri kazanım ünitelerinin çağdaş yapılara nasıl uygulandığını inceliyoruz.",
                ImageUrl = "~/theme/builderz/img/blog-1.jpg",
                Author = "Admin",
                Category = "İnşaat Teknolojileri",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var post2 = new BlogPost
            {
                Title = "Depreme Dayanıklı Yapı Tasarımında 5 Temel İlke",
                Summary = "Modern sismik izolatörler, doğru zemin etüdü ve kaliteli betonarmenin hayati önemi.",
                Content = @"Deprem gerçeğiyle yaşayan coğrafyalarda yapı güvenliği her şeyden önce gelir. Doğru zemin mekaniği analizi, radye temel uygulamaları, yüksek mukavemetli çelik donatılar ve bina taban izolatörleri hayati önem taşır.",
                ImageUrl = "~/theme/builderz/img/blog-2.jpg",
                Author = "Admin",
                Category = "Mühendislik",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            var post3 = new BlogPost
            {
                Title = "İç Mekan Yenilemede 2026 Trendleri ve Fonksiyonel Çözümler",
                Summary = "Doğal ahşap dokuları, minimalist çizgiler ve açık alan mimarisinin yeni nesil yorumları.",
                Content = @"Ev ve ofis alanlarının konforlu hale getirilmesi için aydınlatma, akustik konfor ve ergonomik yerleşim ön planda tutulmalıdır.",
                ImageUrl = "~/theme/builderz/img/blog-3.jpg",
                Author = "Admin",
                Category = "İç Mimari",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            context.BlogPosts.AddRange(post1, post2, post3);
            await context.SaveChangesAsync();

            // Add sample comment
            context.BlogComments.Add(new BlogComment
            {
                BlogPostId = post1.Id,
                Name = "Ahmet Yılmaz",
                Email = "ahmet@example.com",
                Comment = "Çok aydınlatıcı bir yazı olmuş, özellikle akıllı cephe sistemleri kısmını çok beğendim.",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();
        }
    }
}
