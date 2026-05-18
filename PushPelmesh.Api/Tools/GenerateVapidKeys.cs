using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PushPelmesh.Api.Tools
{
    public static class GenerateVapidKeys
    {
        // ВРЕМЕННО для генерации VAPID ключей
        public static void GenerateKeys(this WebApplication app)
        {
            var vapidKeys = WebPush.VapidHelper.GenerateVapidKeys();

            Console.WriteLine("Public Key: " + vapidKeys.PublicKey);
            Console.WriteLine("Private Key: " + vapidKeys.PrivateKey);
        }
    }
}