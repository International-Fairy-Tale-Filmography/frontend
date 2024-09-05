using System;
using System.Collections.Generic;
using System.Text;

namespace DataEditor.Core.Configuration
{
    public class CoreSettingsModel
    {
        public string AccessToken { get; set; }
        public string Owner { get; set; }
        public string RepoName { get; set; } 
        public string Branch { get; set; }
        public string Folder { get; set; }
    }
}
