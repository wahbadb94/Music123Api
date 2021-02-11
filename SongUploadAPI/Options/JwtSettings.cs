﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SongUploadAPI.Options
{
    public class JwtSettings
    {
        public string Secret { get; set; }
        public string UserIdClaimName { get; set; }
    }
}
