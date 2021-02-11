﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SongUploadAPI.Domain
{
    public class AuthenticationResult
    {
        public string Token { get; set; }
        public bool Succeeded { get; set; }
        public bool Failed => !Succeeded;
        public IEnumerable<string> ErrorMessages { get; set; }
    }
}
