﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WaterGate.Models
{
    public static class Settings
    {
        public static string ServiceAddress { get; private set; }
        public static string WebServiceAddress { get; private set; }

        public static void Initialize(string serviceAddress)
        {
            ServiceAddress = serviceAddress;
            if (serviceAddress.StartsWith("http://"))
            {
                WebServiceAddress = serviceAddress;
            }
            else
            {
                WebServiceAddress = "http://" + serviceAddress;
            }
        }

    }
}
