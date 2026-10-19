using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface ITestimonialService
{
    Task<IEnumerable<Testimonial>> GetAllTestimonialsAsync();
}
