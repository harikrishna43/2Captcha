using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace _2Captcha
{
    public class TwoCaptcha
    {
        public string APIKey { get; private set; }
        public TwoCaptcha(string apiKey)
        {
            APIKey = apiKey;
        }


        public bool SolveCaptchaV2(string _filePath, out string result)
        {

            try
            {
                byte[] data = File.ReadAllBytes(_filePath);
                string filename = Path.GetFileName(_filePath);
                // fs.Read(data, 0, data.Length);
                // fs.Close();c31aa3b467f9162a796634dd1321dc63

                // Generate post objects
                Dictionary<string, object> postParameters = new Dictionary<string, object>();
                postParameters.Add("filename", filename);
                postParameters.Add("fileformat", "JPEG");
                postParameters.Add("min_len", "0");
                postParameters.Add("max_len", "0");
                postParameters.Add("header_acao", "0");
                postParameters.Add("Access-Control-Allow-Origin", "*");
                postParameters.Add("file", new FormUpload.FileParameter(data, filename, "application/msword"));

                // Create request and receive response
                string postURL = "http://2captcha.com/in.php?key=" + APIKey;
                string userAgent = APIKey;
                HttpWebResponse webResponse = FormUpload.MultipartFormDataPost(postURL, userAgent, postParameters);

                // Process response
                StreamReader responseReader = new StreamReader(webResponse.GetResponseStream());
                string fullResponse = responseReader.ReadToEnd();
                webResponse.Close();
                // cptcha(fullResponse);
                Console.Write(fullResponse);
                string response = fullResponse;
                if (response.Length < 3)
                {
                    result = response;
                    return false;
                }
                else
                {
                    if (response.Substring(0, 3) == "OK|")
                    {
                        string captchaID = response.Remove(0, 3);

                        for (int i = 0; i < 24; i++)
                        {
                            WebRequest getAnswer = WebRequest.Create("http://2captcha.com/res.php?key="+APIKey+"&action=get&id=" + captchaID);
                            using (WebResponse answerResp = getAnswer.GetResponse())
                            using (StreamReader answerStream = new StreamReader(answerResp.GetResponseStream()))
                            {
                                string answerResponse = answerStream.ReadToEnd();

                                if (answerResponse.Length < 3)
                                {
                                    result = answerResponse;
                                    return false;
                                }
                                else
                                {
                                    if (answerResponse.Substring(0, 3) == "OK|")
                                    {
                                        result = answerResponse.Remove(0, 3);
                                        return true;
                                    }
                                    else if (answerResponse != "CAPCHA_NOT_READY")
                                    {
                                        result = answerResponse;
                                        return false;
                                    }
                                }
                            }

                            Thread.Sleep(5000);
                        }

                        result = "Timeout";
                        return false;
                    }
                    else
                    {
                        result = response;
                        return false;
                    }
                }
            }
            catch { }

            result = "Unknown error";
            return false;
        }
    }

    public static class FormUpload
    {
        private static readonly Encoding encoding = Encoding.UTF8;
        public static HttpWebResponse MultipartFormDataPost(string postUrl, string userAgent, Dictionary<string, object> postParameters)
        {
            string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;

            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            return PostForm(postUrl, userAgent, contentType, formData);
        }
        private static HttpWebResponse PostForm(string postUrl, string userAgent, string contentType, byte[] formData)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null)
            {
                throw new NullReferenceException("request is not a http request");
            }

            // Set up the request properties.
            request.Method = "POST";
            request.ContentType = contentType;
            request.UserAgent = userAgent;
            request.CookieContainer = new CookieContainer();
            request.ContentLength = formData.Length;
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(formData, 0, formData.Length);
                requestStream.Close();
            }

            return request.GetResponse() as HttpWebResponse;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter)
                {
                    FileParameter fileToUpload = (FileParameter)param.Value;

                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\";\r\nContent-Type: {3}\r\n\r\n",
                        boundary,
                        param.Key,
                        fileToUpload.FileName ?? param.Key,
                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                }
                else
                {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                        boundary,
                        param.Key,
                        param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }

        public class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }
        }
    }
}
