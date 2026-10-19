using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Core.Interfaces;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class TestimonialService : ITestimonialService
{
    private readonly IUnitOfWork _unitOfWork;

    public TestimonialService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Testimonial>> GetAllTestimonialsAsync()
    {
        return await _unitOfWork.Repository<Testimonial>().GetAllAsync();
    }
}
