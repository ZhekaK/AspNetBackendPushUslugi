using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Dtos
{
    public class CreateUserRoleRequest
    {
        public UserPost Post { get; set; }
        public string? PostName { get; set; }
        public string? GivePlace { get; set; }
        public DateOnly StartDate { get; set; }
        public string UserSeries { get; set; } = "";
        public string UserNumbers { get; set; } = "";
    }
}