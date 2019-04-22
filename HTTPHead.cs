using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace lab4
{
    public class HTTPHead
    {
        public string methodOrVer { get; set; }
        public string requestURIOrStatus { get; set; }
        public string verOrStatus { get; set; }
        public LinkedList<NameValueHeader> headers;
        public string host; //duplicate, but for convinience
        public int port;

        public HTTPHead() { }

        public HTTPHead(string methodOrVer, string requestURIOrStatus, string verOrStatus)
        {
            this.methodOrVer = methodOrVer;
            this.requestURIOrStatus = requestURIOrStatus;
            this.verOrStatus = verOrStatus;
            this.headers = new LinkedList<NameValueHeader>();
        }

    }
}
