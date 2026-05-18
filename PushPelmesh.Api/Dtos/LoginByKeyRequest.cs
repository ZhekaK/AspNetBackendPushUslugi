using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PushPelmesh.Api.Dtos
{
    public class LoginByKeyRequest
    {
        public string Series { get; set; } = "";

        public string Number { get; set; } = "";
    }
}