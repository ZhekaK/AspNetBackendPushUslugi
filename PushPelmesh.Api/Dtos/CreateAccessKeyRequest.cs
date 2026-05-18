using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Dtos
{
    public class CreateAccessKeyRequest
    {
        public string Series { get; set; } = "";

        public string FirstName { get; set; } = "";

        public string? MiddleName { get; set; }

        public string? LastName { get; set; }
        public DateOnly? BirthDate { get; set; }
        public DateOnly? GiveDate { get; set; }
        public string? GivePlace { get; set; }
        public Sex? Sex { get; set; }
    }
}