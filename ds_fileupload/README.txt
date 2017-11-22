HOW TO RUN THE ASSET UPLOADER APP

1. Install .Net Core.  It can be installed on Windows, Linux, or MacOS.  
These instructions are targeted primarily at a Windows installation. 
See the URL -- https://www.microsoft.com/net/learn/get-started/windows

The success of the installation can be checked by bringing up a Windows Command 
prompt and typing "dotnet --version".  

2. Unzip the file "ds_fileupload.zip" into a local directory.

3. Edit the file "appsettings.json" in the ds_fileupload directory.  Replace the 
four values in the "AWSCreds" section with the values associated with the targeted AWS S3
service.

4. Bring up a command prompt window and "cd C:\{path}\ds_fileupload", where "path" is the 
folder path containing the ds_fileupload directory.

5. Start the app by typing "dotnet ds_fileupload.dll".  The app can be quickly tested using
curl or Google's Postman.  The base URL will most likely be "http://localhost:5000".  For 
example, a POST could be sent to the URL "http://localhost:5000/asset" with an empty body 
and a Content-Type "application/json".  It should return a JSON structure.  

HOW TO RUN THE UNIT TESTS

1. The tests also require a .Net Core installation on the local system.  

2. Unzip the file "ds_unittests.zip" into a local directory.

3. Edit the file "baseurl.txt" in the ds_unitests directory and if necessary, replace the
base URL.

4. Bring up a command prompt window and "cd C:\{path}\ds_unittests", where "path" is the 
folder path created for the tests.

5. Start the tests by typing "dotnet vstest ds_unittests.dll".  The tests results will be 
reported in the command prompt window and should be successful.  There should also be a new
file in the AWS bucket which can be checked in the AWS Console.  

NOTE:  Normally, a test set should return the state of its target to an initial state, in 
this case by deleting files ithad uploaded.  In this case however, leaving the files is 
useful check on the operation of the tests

SOURCE CODE

The source is contained primarily in the file Controllers/AssetController.cs.  

