using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System;
using System.Threading.Tasks;
using System.Text;

namespace ds_unittests
{
    [TestClass]
    public class UnitTest1
    {
        static string BaseUrl = readBaseUrl();
        static string ContentString = "test";
        static byte[] barray = Encoding.ASCII.GetBytes(ContentString);
        static int ContentLength = barray.Length;
        static HttpClient client = null;
        static string upload_url = null;
        static string asset_id = null;
        static bool status = false;
        static bool uploaded = false;
        static string download_url = null;


        [TestMethod]
        public void PostGetURL()
        {
            testinit();
            clientinit();
            Task<HttpResponseMessage> response = client.PostAsync("/asset", null);

            Assert.IsTrue (response.Result.IsSuccessStatusCode);

            Task<Filestore> tfs = response.Result.Content.ReadAsAsync<Filestore>();
            Assert.IsNotNull(tfs.Result);
            Filestore fs = tfs.Result;
            upload_url = fs.upload_url;
            asset_id = fs.id;
        }      

        [TestMethod]
        public void GetBadAsset()
        {         
            clientinit();
            Task<HttpResponseMessage> response = client.GetAsync("/asset/11111111");

            Assert.IsFalse(response.Result.IsSuccessStatusCode);
        }

        [TestMethod]
        public void UploadAsset()
        {
            HttpContent content = new ByteArrayContent(barray);
            if (asset_id == null || upload_url == null)
            {
                PostGetURL();             
            }
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            Task<HttpResponseMessage> response = client.PostAsync(upload_url, content);
            Assert.IsTrue(response.Result.IsSuccessStatusCode);
            uploaded = true;
        }

        [TestMethod]
        public void GetBadStatus()
        {
            if (!uploaded)
                UploadAsset();
            clientinit();
            Task<HttpResponseMessage> response = client.GetAsync("/asset/" + asset_id + "?timeout=100");

            Assert.IsFalse(response.Result.IsSuccessStatusCode);                
        }

        [TestMethod]
        public void PutUploadedStatus()
        {
            if (!uploaded)
                UploadAsset();
            clientinit();
            Status_response sr = new Status_response();
            sr.Status = "uploaded";
            Task<HttpResponseMessage> response = client.PutAsJsonAsync("/asset/" + asset_id, sr);
            Assert.IsTrue(response.Result.IsSuccessStatusCode);
            status = true;
        }
        [TestMethod]
        public void GetDownloadUrl ()
        {
            if (!status)
                PutUploadedStatus();          
            clientinit();
            Task<HttpResponseMessage> response = client.GetAsync("/asset/" + asset_id + "?timeout=100");
            Task<Download_response> tfs = response.Result.Content.ReadAsAsync<Download_response>();
            Assert.IsTrue(response.Result.IsSuccessStatusCode);
            Assert.IsNotNull(tfs.Result);
            Download_response dr = tfs.Result;
            download_url = dr.Download_url;
        }

        [TestMethod]
        public void GetAsset()
        {
            if (download_url == null)
                GetDownloadUrl();
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            Task<HttpResponseMessage> response = client.GetAsync(download_url);
            Assert.IsTrue(response.Result.IsSuccessStatusCode);
            Assert.AreEqual(response.Result.Content.Headers.ContentLength, ContentLength);
        }     

        private void clientinit ()
        {
            Assert.IsNotNull(BaseUrl);
            if (client != null)
                client.Dispose();
            client = new HttpClient();
            client.BaseAddress = new Uri(BaseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private void testinit()
        {
            uploaded = false;
            upload_url = null;
            download_url = null;
            asset_id = null;
            status = false;
        }

        static private string readBaseUrl()
        {
            System.IO.StreamReader file = new System.IO.StreamReader(AppDomain.CurrentDomain.BaseDirectory + @"baseurl.txt");
            string line = file.ReadLine();
            file.Close();
            return line;
        }
    }
}

