using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PushPelmesh.Api.Models
{
    public enum UserPost
    {
        None = 0,
        Minister = 1,
        Governor = 2,
        President = 3
    }
    public class UserRole
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";
        public UserPost Post { get; set; }
        public string? PostName { get; set; }
        public string? GivePlace { get; set; }
        public DateOnly StartDate { get; set; }
        public string? UserSeries { get; set; }
        public string? UserNumbers { get; set; }
    }
}