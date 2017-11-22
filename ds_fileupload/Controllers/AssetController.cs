using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ds_fileupload.Controllers
{
    [Route("/asset")]
    public class AssetController : Controller
    {       
        static string awsAccessKey = null; 
        static string awsSecretAccessKey = null; 
        static string bucketName = null; 
        static string get_timeout = "60";
        static Amazon.RegionEndpoint awsregionep;

        static void initcreds ()
        {
            var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json");

            var config = builder.Build();

            var appConfig = new AppOptions();
            config.GetSection("AWSCreds").Bind(appConfig);
            awsAccessKey = appConfig.access_key;
            awsSecretAccessKey = appConfig.secret_access_key;
            awsregionep = Amazon.RegionEndpoint.GetBySystemName(appConfig.region);
            bucketName = appConfig.bucketname;
        }
                  
       
        [HttpGet()]
        public void Get() 
        {
        }

        //GET (asset/id)  This method returns a signed url that can be used to download an asset
        // from AWS S3.
        // 1. If a timeout query parameter exists, apply the value to the expiration of the 
        //    signed url for the GET operation.
        // 2. Check the Status tag for the asset.  If the value is not "uploaded" return an error.
        // 3. Get the s3 signed url and return it.  
        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            IAmazonS3 s3Client;
            Download_response dr = new Download_response();
            HttpRequest request = HttpContext.Request;
            string query = request.Query["timeout"].ToString();
            int timeint;
            // If a "timeout" query exists and it can be parsed correctly as a number,
            // apply the timeout value to the expiration timeout of the signed url.
            if (query != null && Int32.TryParse(query, out timeint))
            {
                get_timeout = query;
            }
               
            try
            {
                if (awsAccessKey == null)
                    initcreds();
                using (s3Client = new AmazonS3Client(awsAccessKey, awsSecretAccessKey, awsregionep))
                {
                    //check Status tag.  If value is not "uploaded", return an error
                    var tagresult = gets3tag(s3Client, id, "Status");
                    string tagval = tagresult.Result;
                    if (tagval == null || tagval != "uploaded")
                    {
                        return NotFound(new ErrorMessage { Error = "File has not been uploaded" });
                    }
                    string urlString = GeneratePreSignedURL(s3Client, id, "get", get_timeout);
                    dr.Download_url = urlString;
                }
                return Ok(dr);
            } catch (Exception )
            {
                return BadRequest();
            }
        }

        // POST: This method
        // 1. Generates a UUID which will be the value of the Asset-id.  
        // 2. Get an AWS S3 signed url which can be shared with others 
        // 3. Wrap the signed url in a query parameter attached to a POST URL
        //    back to this app.  The AWS S3 signed url seems to expect a PUT 
        //    method rather than a POST.  This app will expose a POST method 
        //    and will then proxy the operation using a PUT to the AWS service.
        [HttpPost]
        public IActionResult Post()
        {
            IAmazonS3 s3Client;
            Filestore fs = new Filestore();
            // Generate UUID/Asset-id
            Guid g = Guid.NewGuid();
            // TODO: should issue a GET to check for name collision on "asset-id" (which should almost never happen)
            fs.id = g.ToString();
            try
            {
                if (awsAccessKey == null)
                    initcreds();
                // Get a s3 signed url
                using (s3Client = new AmazonS3Client(awsAccessKey, awsSecretAccessKey, awsregionep))
                {
                    string urlString = GeneratePreSignedURL(s3Client, fs.id, "put");
                    fs.upload_url = urlString;
                }
                // Wrap the signed url in a query parameter attached to a POST URL for this app
                string localurl = HttpContext.Request.Host.Value;
                string scheme = HttpContext.Request.Scheme;
                fs.upload_url = scheme + "://" + localurl + "/asset/" + fs.id + "?redir=" + WebUtility.UrlEncode(fs.upload_url);
                return Ok(fs);
            }
            catch (Exception )
            {
                return StatusCode((int)HttpStatusCode.InternalServerError);
            }
        }

        //POST (/asset/ID -- This method proxies the upload operation to AWS S3
        // 1. Extract the AWS signed url from the query string
        // 2. Copy the body from the Request Stream to a temporary file
        // 3. Create a PUT request to the AWS service and copy the contents of the 
        //    temporary file to the request body
        // 4. Send the upload request to AWS using the signed url
        [HttpPost("{id}")]
        public IActionResult Postid(string id)
        {
            try
            {
                HttpRequest request = HttpContext.Request;
                string query = request.Query["redir"].ToString();
                string url = WebUtility.UrlDecode(query);
                UploadAsset(url, request);
                return Ok();
            }
            catch (Exception )
            {
                return StatusCode((int)HttpStatusCode.InternalServerError);
            }
        }

        // PUT (asset/id)  This method
        // 1. Sets a Status Tag on the AWS S3 file.  The tag name is "Status" and the value
        //    will typically be "uploaded".
        [HttpPut("{id}")]
        public IActionResult Put(string id, [FromBody] Status_response sr)
        {
            try
            {
                var r = sets3tag(id, "Status", sr.Status);
                bool success = r.Result;
                if (success)
                    return Ok();
                else
                    return BadRequest();
            }
            catch (Exception )
            {
                return BadRequest("no body");
            }
        }

        // Following code adapted from http://docs.aws.amazon.com/AmazonS3/latest/dev/ShareObjectPreSignedURLDotNetSDK.html
        static string GeneratePreSignedURL(IAmazonS3 s3Client, string objectKey, string verbparam)
        {
            return GeneratePreSignedURL(s3Client, objectKey, verbparam, "300");
        }
        static string GeneratePreSignedURL(IAmazonS3 s3Client, string objectKey, string verbparam, string seconds)
        {
            HttpVerb verb;
            if (verbparam == "put")
                verb = HttpVerb.PUT;
            else
                verb = HttpVerb.GET;

            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Verb = verb,
                Expires = DateTime.Now.AddSeconds(Convert.ToInt32(seconds))
            };

            string url = null;         
            url = s3Client.GetPreSignedURL(request);          
            return url;
        }
        static void UploadAsset(string url, HttpRequest request)
        {

            string filePath = Path.GetTempFileName();
            using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                request.Body.CopyTo(fs);
            }

            HttpWebRequest httpRequest = WebRequest.Create(url) as HttpWebRequest;
            httpRequest.Method = "PUT";
            using (Stream dataStream = httpRequest.GetRequestStream())
            {
                byte[] buffer = new byte[8000];
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int bytesRead = 0;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        dataStream.Write(buffer, 0, bytesRead);
                    }
                }
            }
            HttpWebResponse response = httpRequest.GetResponse() as HttpWebResponse;
        }

        private async Task<bool> sets3tag(string id, string newkey, string newvalue)
        {
            IAmazonS3 s3Client;
            Tagging newTagSet = new Tagging();
            newTagSet.TagSet = new List<Tag>{
                    new Tag { Key = newkey, Value = newvalue}
                };
            if (awsAccessKey == null)
                initcreds();
            using (s3Client = new AmazonS3Client(awsAccessKey, awsSecretAccessKey, awsregionep))
            {
                PutObjectTaggingRequest putObjTagsRequest = new PutObjectTaggingRequest();
                putObjTagsRequest.BucketName = bucketName;
                putObjTagsRequest.Key = id;
                putObjTagsRequest.Tagging = newTagSet;

                PutObjectTaggingResponse response = await s3Client.PutObjectTaggingAsync(putObjTagsRequest);
                if (response.HttpStatusCode == HttpStatusCode.OK)
                    return true;
            }
            return false;
        }

        private async Task<string> gets3tag(IAmazonS3 s3Client, string id, string newkey)
        {
            GetObjectTaggingRequest getTagsRequest = new GetObjectTaggingRequest();
            getTagsRequest.BucketName = bucketName;
            getTagsRequest.Key = id;
            GetObjectTaggingResponse objectTags = await s3Client.GetObjectTaggingAsync(getTagsRequest);

            Tag tag = objectTags.Tagging.Find(t => t.Key == "Status");
            if (tag != null)
                return tag.Value;
            else
                return null;
        }
    }
}

public class Filestore
{
    public string upload_url { get; set; }
    public string id { get; set; }
}
public class Status_response
{
    public string Status { get; set; }
}
public class Download_response
{
    public string Download_url { get; set; }
}

public class ErrorMessage
{
    public string Error { get; set; }
}

public class AppOptions
{
    public string access_key { get; set; }
    public string secret_access_key { get; set; }
    public string region { get; set; }
    public string bucketname { get; set; }
}