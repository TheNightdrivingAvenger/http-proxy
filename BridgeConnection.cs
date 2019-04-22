using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace lab4
{
    class BridgeConnection
    {

        public static string[] forbiddenHosts;
        public static string fobiddenPage;

        private Socket toServer;
        private Socket toBrowser;
        byte[] requestBuf;
        byte[] responseBuf;
        private int lexerPos;
        private string curLexerString;
        private int requestBufEnd;
        private int responseBufEnd;

        private HTTPHead requestHead;
        private HTTPHead responseHead;

        public Thread toBrowserThread;
        public Thread toServerThread;

        public BridgeConnection(Socket toBrowserClient)
        {
            toBrowser = toBrowserClient;
            requestBuf = new byte[1024 * 1024];
            responseBuf = new byte[1024 * 1024];
            lexerPos = 0;
            requestBufEnd = 0;
            responseBufEnd = 0;

            //PORT = 80;
            //head = new HTTPHead();
        }

        public void StartServing()
        {
            toBrowserThread = new Thread(ToBrowserConnection);
            toBrowserThread.Start();
        }

        private void ToBrowserConnection()
        {
            bool gotHead = false, error = false;
            bool shutted = false;
            int readRes;
            while (!gotHead)
            {
                try
                {
                    readRes = toBrowser.Receive(requestBuf, requestBufEnd, requestBuf.Length - requestBufEnd, SocketFlags.None);
                    requestBufEnd += readRes;
                    if (readRes == 0)
                    {
                        error = true;
                        break;
                    }
                }
                catch (SocketException ex)
                {
                    error = true;
                    break;
                }

                if (requestBufEnd == requestBuf.Length)
                {
                    // header is too long, just sever the connection...
                    error = true;
                    break;
                }
                else
                {
                    try
                    {
                        ParseHead(ref requestHead, requestBuf);
                        if (requestHead != null)
                        {
                            gotHead = true;

                            var hostHeader = FindHost();
                            requestHead.host = hostHeader.Value.Split(':')[0];
                            requestHead.port = GetPortIfPresent(hostHeader);

                            requestHead.port = (requestHead.port == 0) ? 80 : requestHead.port;

                            if (Array.IndexOf(forbiddenHosts, requestHead.host) >= 0)
                            {
                                Console.WriteLine("Denied attempt of connection to {0}", requestHead.host);
                                ReplyWithForbidden();
                                CloseAll();
                                return;
                            }
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        error = true;
                        break;
                    }
                }
            }

            if (error)
            {
                CloseAll();
                return;
            }

            toServer = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                toServer.Connect(requestHead.host, requestHead.port);
            }
            catch (SocketException ex)
            {
                CloseAll();
                return;
            }
            Console.WriteLine("Connected to {0}:{1}; {2} {3} {4}", requestHead.host, requestHead.port, requestHead.methodOrVer,
                requestHead.requestURIOrStatus, requestHead.verOrStatus);
            toServerThread = new Thread(ToServerConnection);
            toServerThread.Start();

            while (!shutted)
            {
                try
                {
                    toServer.Send(requestBuf, 0, requestBufEnd, SocketFlags.None);
                    requestBufEnd = toBrowser.Receive(requestBuf, 0, requestBuf.Length, SocketFlags.None);
                    if (requestBufEnd == 0)
                    {
                        CloseAll();
                        shutted = true;
                    }
                }
                catch (SocketException ex)
                {
                    CloseAll();
                    shutted = true;
                }
                catch (ObjectDisposedException)
                {
                    CloseAll();
                    shutted = true;
                }
            }
        }

        private void ToServerConnection()
        {
            bool shutted = false, gotHead = false, error = false;

            int readRes;
            while (!gotHead)
            {
                try
                {
                    readRes = toServer.Receive(responseBuf, responseBufEnd, responseBuf.Length - responseBufEnd, SocketFlags.None);
                    responseBufEnd += readRes;
                    if (readRes == 0)
                    {
                        error = true;
                        Console.WriteLine("{0}:{1} has closed the connection.", requestHead.host, requestHead.port);
                        break;
                    }
                }
                catch (SocketException ex)
                {
                    error = true;
                    Console.WriteLine("{0}:{1} aborted the connection", requestHead.host, requestHead.port);
                    break;
                }
                catch (ObjectDisposedException)
                {
                    error = true;
                    Console.WriteLine("Browser closed/aborted the connection with {0}:{1}", requestHead.host, requestHead.port);
                    break;
                }

                if (responseBufEnd == responseBuf.Length)
                {
                    // header is too long, just sever the connection...
                    error = true;
                    break;
                }
                else
                {
                    try
                    {
                        ParseHead(ref responseHead, responseBuf);
                        if (responseHead != null)
                        {
                            gotHead = true;
                            Console.WriteLine("First response from {0}:{1}; {2} {3} {4}", requestHead.host, requestHead.port,
                                responseHead.methodOrVer, responseHead.requestURIOrStatus, responseHead.verOrStatus);
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        error = true;
                        break;
                    }
                }
            }

            if (error)
            {
                CloseAll();
                return;
            }


            while (!shutted)
            {
                try
                {
                    toBrowser.Send(responseBuf, 0, responseBufEnd, SocketFlags.None);
                    responseBufEnd = toServer.Receive(responseBuf, 0, responseBuf.Length, SocketFlags.None);
                    if (responseBufEnd == 0)
                    {
                        CloseAll();
                        Console.WriteLine("{0}:{1} has closed the connection.", requestHead.host, requestHead.port);
                        shutted = true;
                    }
                }
                catch (SocketException ex)
                {

                    CloseAll();
                    Console.WriteLine("{0}:{1} aborted the connection", requestHead.host, requestHead.port);

                    shutted = true;
                }
                catch (ObjectDisposedException)
                {
                    CloseAll();
                    Console.WriteLine("Browser closed/aborted the connection with {0}:{1}", requestHead.host, requestHead.port);

                    shutted = true;
                }
            }
        }

        private void ParseHead(ref HTTPHead head, byte[] buf)
        {
            bool gotHead = false;
            while (!gotHead && (lexerPos + 3 < requestBufEnd))
            {
                if (!((char)buf[lexerPos] == '\r' && (char)buf[lexerPos + 1] == '\n' &&
                    (char)buf[lexerPos + 2] == '\r' && (char)buf[lexerPos + 3] == '\n'))
                {
                    curLexerString += (char)buf[lexerPos];
                    lexerPos++;
                } else
                {
                    gotHead = true;
                }
            }

            if (!gotHead)
            {
                // shift 4 chars back and try again next time, when more data is available
                lexerPos = Math.Max(lexerPos - 4, 0);
                return;
            }

            curLexerString += "\r\n";

            var sep = new string[1];
            sep[0] = "\r\n";

            string[] headers = curLexerString.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            head = InitRequestLine(headers[0]);
            if (head == null)
            {
                throw new ArgumentException("Request line could not be found\r\n");
            }

            for (int i = 1; i < headers.Length; i++)
            {
                var header = ParseHeader(headers[i]);
                if (header != null)
                {
                    head.headers.AddLast(header);
                }
                else
                {
                    throw new ArgumentException("One or more of the headers was invalid\r\n");
                }
            }

            curLexerString = "";
            lexerPos = 0;
        }

        private NameValueHeader FindHost()
        {
            foreach (var pair in requestHead.headers)
            {
                if (pair.Name.Equals("host", StringComparison.OrdinalIgnoreCase))
                {
                    return pair;
                }
            }
            return null;
        }

        private int GetPortIfPresent(NameValueHeader pair)
        {
            var hostPort = pair.Value.Split(':');
            if (hostPort.Length == 2)
            {
                int result;
                if (Int32.TryParse(hostPort[1], out result))
                {
                    return result;
                } else
                {
                    throw new ArgumentException("Port has an invalid value");
                }
            }
            else
            {
                return 0;
            }
        }

        private HTTPHead InitRequestLine(string line)
        {
            line = Regex.Replace(line, @"\s+", " ");
            string[] parameters = line.Split(null, 3);

            // if not HTTP/1.1/0 request/response, treat it as invalid
            if (!((parameters[0] == "HTTP/1.1") ^ (parameters[2] == "HTTP/1.1") ^
                (parameters[0] == "HTTP/1.0") ^ (parameters[2] == "HTTP/1.0")))
            {
                return null;
            }

            return new HTTPHead(parameters[0], parameters[1], parameters[2]);
        }

        private NameValueHeader ParseHeader(string curHeader)
        {
            var sep = new char[1];
            sep[0] = ':';
            string[] nameValue = curHeader.Split(sep, 2);
            return new NameValueHeader(nameValue[0].Trim(), nameValue[1].Trim());
        }

        private void ReplyWithForbidden()
        {
            try
            {
                toBrowser.Send(Encoding.UTF8.GetBytes(Regex.Replace(fobiddenPage, "{DOMAIN}", requestHead.host)));
            }
            catch
            {
                // ignore the exceptions, because if something happened, there's nothing I can do;
                // and the connection will be closed anyways just after the ReplyWithForbidden() call
            }
        }

        private void CloseAll()
        {
            if (toBrowser != null)
            {
                toBrowser.Close();
            }
            if (toServer != null)
            {
                toServer.Close();
            }
        }
    }
}
