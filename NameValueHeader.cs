using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab4
{
    public class NameValueHeader
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        public NameValueHeader(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }
    }
}
