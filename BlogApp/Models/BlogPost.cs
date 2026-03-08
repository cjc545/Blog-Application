using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlogApp.Models
{
    public class BlogPost
    {
        public int ID { get; set; }

        [Required(ErrorMessage ="Title is Required!!!")]
        [StringLength(100,ErrorMessage ="Title cannot exceed 100 character")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Blog Contents is Required!!!")]
        [MinLength(10, ErrorMessage = "Content must at least be 10 character long")]
        public string Content { get; set; }

        [Required(ErrorMessage = "Blog Article Published Date is Required!!!")]
        [DataType(DataType.Date)]
        [CustomValidation(typeof(BlogPost), nameof(ValidatePublishedDate))]
        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;
        public string? ImagePath { get; set; }

        [NotMapped]
        public IFormFile? ImageFile { get; set; }

        // Foreign Key
        public int UserId { get; set; }
        public User User { get; set; }
        public ICollection<Comments> Comments { get; set; }

        public static ValidationResult ValidatePublishedDate(DateTime publishedDate, ValidationContext context)
        {
            if (publishedDate > DateTime.UtcNow)
            {
                return new ValidationResult("Published date cannot be in the future!!!");
            }
            return ValidationResult.Success;
        }
    }

    
}
