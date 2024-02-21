using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Syncfusion.Blazor.FileManager;
using Amazon;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.IO;
namespace FileManager_FlatData.Data
{
    public class AmazonService
    {
        public static string bucketName = "syncfusion-filemanager";
        public static IAmazonS3? client;
        public static ListObjectsResponse? response;
        public ListObjectsResponse? initialResponse;
        static ListObjectsResponse? childResponse;
        public string? RootName;
        private string rootName = string.Empty;
        private string accessMessage = string.Empty;
        AccessDetails AccessDetails = new AccessDetails();
        long sizeValue = 0;
        List<FileManagerDirectoryContent> s3ObjectFiles = new List<FileManagerDirectoryContent>();
        TransferUtility fileTransferUtility = new TransferUtility(client);
        public async Task<FileManagerResponse<FileManagerDirectoryContent>> GetFiles(string path, FileManagerDirectoryContent[] data)
        {
            FileManagerResponse<FileManagerDirectoryContent> Response = new FileManagerResponse<FileManagerDirectoryContent>();
            await RegisterAmazonS3("syncfusion-filemanager", "AKIAWH6GYCX36OQETWF4", "o/kJzgMAHyOuXBVjRpIEYx3jBkBxuxLrHGjJRfK5", "us-east-1");
            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            List<FileManagerDirectoryContent> filesS3 = new List<FileManagerDirectoryContent>();
            await GetBucketList();
            try
            {
                if (path == "/") await ListingObjectsAsync("/", RootName, false); else await ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false);
                if (path == "/")
                {
                    List<FileManagerDirectoryContent> s = new List<FileManagerDirectoryContent>();
                    foreach (var obj in response.S3Objects.Where(x => x.Key == RootName))
                    {
                        var file = await CreateDirectoryContentInstance(obj.Key.ToString().Replace("/", ""), false, "Folder", obj.Size, obj.LastModified, obj.LastModified, this.checkChild(obj.Key), string.Empty);
                        s.Add(file);
                    }
                    FileManagerDirectoryContent[] resultArray = s.ToArray();
                    if (s.ToArray().Length > 0) cwd = s[0];
                }
                else
                    cwd = await CreateDirectoryContentInstance(path.Split("/")[path.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, Task.FromResult((response.CommonPrefixes.Count > 0) ? true : false), path.Substring(0, path.IndexOf(path.Split("/")[path.Split("/").Length - 2])));
            }
            catch (Exception ex) { throw ex; }
            try
            {
                if (response.CommonPrefixes.Count > 0)
                {
                    foreach (var prefix in response.CommonPrefixes)
                    {
                        var file = await CreateDirectoryContentInstance(getFileName(prefix, path), false, "Folder", 0, DateTime.Now, DateTime.Now, this.checkChild(prefix), getFilePath(prefix));
                        files.Add(file);
                    }
                }
            }
            catch (Exception ex) { throw ex; }
            try
            {
                if (path == "/") await ListingObjectsAsync("/", RootName, false); else await ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false);
                if (response.S3Objects.Count > 0) 
                { 
                    foreach (var obj in response.S3Objects.Where(x => x.Key != RootName.Replace("/", "") + path))
                    {
                        var file = await CreateDirectoryContentInstance(obj.Key.ToString().Replace(RootName.Replace("/", "") + path, "").Replace("/", ""), true, Path.GetExtension(obj.Key.ToString()), obj.Size, obj.LastModified, obj.LastModified, this.checkChild(obj.Key), getFilterPath(obj.Key, path));
                        filesS3.Add(file);
                    }
                }
            }
            catch (Exception ex) { throw ex; }
            if (filesS3.Count != 0) files = files.Union(filesS3).ToList();
            Response.CWD = cwd;
            try
            {
                if ((cwd.Permission != null && !cwd.Permission.Read))
                {
                    Response.Files = null;
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + cwd.Name + "' is not accessible. You need permission to perform the read action.");
                }
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                Response.Error = er;
            }
            Response.Files = files;
            return Response;
        }

        // Delete aync method
        public virtual async Task AsyncDelete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse<FileManagerDirectoryContent> Response = new FileManagerResponse<FileManagerDirectoryContent>();
            try
            {
                List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
                await GetBucketList();
                if (path == "/") await ListingObjectsAsync("/", RootName, false); else await ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false);
                foreach (string name in names)
                {
                    foreach (FileManagerDirectoryContent item in data)
                    {
                        AccessPermission PathPermission = GetPathPermission(item.FilterPath + item.Name, data[0].IsFile);
                        if (PathPermission != null && (!PathPermission.Read || !PathPermission.Write))
                        {
                            accessMessage = PathPermission.Message;
                            throw new UnauthorizedAccessException("'" + name + "' is not accessible.  You need permission to perform the write action.");
                        }
                    }
                    if (response.CommonPrefixes.Count > 1)
                    {
                        foreach (string commonPrefix in response.CommonPrefixes)
                        {
                            if (commonPrefix == this.RootName.Replace("/", "") + path + name)
                                files.Add(await CreateDirectoryContentInstance(commonPrefix.Split("/")[commonPrefix.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, Task.FromResult(false), ""));
                        }
                    }
                    if (response.S3Objects.Count > 1)
                    {
                        foreach (S3Object S3Object in response.S3Objects)
                        {
                            if (S3Object.Key == this.RootName.Replace("/", "") + path + name)
                                files.Add(await CreateDirectoryContentInstance(S3Object.Key.Split("/").Last(), true, Path.GetExtension(S3Object.Key), S3Object.Size, S3Object.LastModified, S3Object.LastModified, Task.FromResult(false), ""));
                        }
                    }
                }
                await DeleteDirectory(path, names);
                Response.Files = files;
            }
            catch (Exception ex)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = ex.Message.ToString();
                er.Code = er.Message.Contains(" is not accessible.  You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                Response.Error = er;
            }
        }

        public async Task DeleteDirectory(string path, string[] names)
        {
            try
            {
                await GetBucketList();
                DeleteObjectsRequest deleteObjectsRequest = new DeleteObjectsRequest() { BucketName = bucketName };
                foreach (string name in names)
                {
                    ListObjectsRequest listObjectsRequest = new ListObjectsRequest { BucketName = bucketName, Prefix = RootName.Replace("/", "") + path + name + (String.IsNullOrEmpty(Path.GetExtension(name)) ? "/" : ""), Delimiter = String.IsNullOrEmpty(Path.GetExtension(name)) ? null : "/" };
                    ListObjectsResponse listObjectsResponse = await client.ListObjectsAsync(listObjectsRequest);
                    foreach (S3Object s3Object in listObjectsResponse.S3Objects) { deleteObjectsRequest.AddKey(s3Object.Key); }
                }
                await client.DeleteObjectsAsync(deleteObjectsRequest);
                await ListingObjectsAsync("/", RootName.Replace("/", "") + path + names[0], false);
                foreach (string name in names)
                {
                    string tempfile = Path.Combine(Path.GetTempPath(), name);
                    if (System.IO.File.Exists(tempfile)) System.IO.File.Delete(tempfile); else if (Directory.Exists(tempfile)) Directory.Delete(tempfile, true);
                }
            }
            catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
        }

        public async Task RegisterAmazonS3(string name, string awsAccessKeyId, string awsSecretAccessKey, string region)
        {
            bucketName = name;
            RegionEndpoint bucketRegion = RegionEndpoint.GetBySystemName(region);
            client = new AmazonS3Client(awsAccessKeyId, awsSecretAccessKey, bucketRegion);
            await GetBucketList();
        }

        public async Task GetBucketList()
        {
            await ListingObjectsAsync("", "", false);
            RootName = response.S3Objects.Where(x => x.Key.Split(".").Length != 2).First().Key;
            RootName = RootName.Replace("../", "");
        }

        public static async Task ListingObjectsAsync(string delimiter, string prefix, bool childCheck)
        {
            try
            {
                ListObjectsRequest request = new ListObjectsRequest { BucketName = bucketName, Delimiter = delimiter, Prefix = prefix };
                if (childCheck)
                    childResponse = await client.ListObjectsAsync(request);
                else
                    response = await client.ListObjectsAsync(request);
            }
            catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
        }

        public string getFilterPath(string fullPath, string path)
        {
            string name = fullPath.ToString().Replace(RootName.Replace("/", "") + path, "").Replace("/", "");
            int nameIndex = fullPath.LastIndexOf(name);
            fullPath = fullPath.Substring(0, nameIndex);
            int rootIndex = fullPath.IndexOf(RootName.Substring(0, RootName.Length - 1));
            fullPath = fullPath.Substring(rootIndex + RootName.Length - 1);
            return fullPath;
        }

        private string getFilePath(string pathString)
        {
            return pathString.Substring(0, pathString.Length - pathString.Split("/")[pathString.Split("/").Length - 2].Length - 1).Substring(RootName.Length - 1);
        }

        private string getFileName(string fileName, string path)
        {
            return fileName.Replace(RootName.Replace("/", "") + path, "").Replace("/", "");
        }

		public async Task<bool> checkChild(string path) // Add async and Task<bool>
		{
			try { await ListingObjectsAsync("/", path, true); } catch (AmazonS3Exception) { throw; }
			return childResponse?.CommonPrefixes.Count > 0 ? true : false;
		}

        private async Task<FileManagerDirectoryContent> CreateDirectoryContentInstance(string name, bool value, string type, long size, DateTime createddate, DateTime modifieddate, Task<bool> child, string filterpath)
        {
            FileManagerDirectoryContent tempFile = new FileManagerDirectoryContent();
            tempFile.Name = name;
            tempFile.IsFile = value;
            tempFile.Type = type;
            tempFile.Size = size;
            tempFile.DateCreated = createddate;
            tempFile.DateModified = modifieddate;
            tempFile.HasChild = await child;
            tempFile.FilterPath = filterpath;
            tempFile.Permission = GetPathPermission(filterpath + (value ? name : Path.GetFileNameWithoutExtension(name)), value);
            return await Task.FromResult(tempFile);
        }

        protected virtual AccessPermission GetPathPermission(string path, bool isFile)
        {
            string[] fileDetails = GetFolderDetails(path);
            if (isFile)
            {
                return GetPermission(fileDetails[0].TrimStart('/') + fileDetails[1], fileDetails[1], true);
            }
            return GetPermission(fileDetails[0].TrimStart('/') + fileDetails[1], fileDetails[1], false);

        }

        protected virtual AccessPermission GetPermission(string location, string name, bool isFile)
        {
            AccessPermission permission = new AccessPermission();
            if (!isFile)
            {
                if (this.AccessDetails.AccessRules == null) { return null; }
                foreach (AccessRule folderRule in AccessDetails.AccessRules)
                {
                    if (folderRule.Path != null && folderRule.IsFile == false && (folderRule.Role == null || folderRule.Role == AccessDetails.Role))
                    {
                        if (folderRule.Path.IndexOf("*") > -1)
                        {
                            string parentPath = folderRule.Path.Substring(0, folderRule.Path.IndexOf("*"));
                            if ((location).IndexOf((parentPath)) == 0 || parentPath == "")
                            {
                                permission = UpdateFolderRules(permission, folderRule);
                            }
                        }
                        else if ((folderRule.Path) == (location) || (folderRule.Path) == (location + Path.DirectorySeparatorChar) || (folderRule.Path) == (location + "/"))
                        {
                            permission = UpdateFolderRules(permission, folderRule);
                        }
                        else if ((location).IndexOf((folderRule.Path)) == 0)
                        {
                            permission = UpdateFolderRules(permission, folderRule);
                        }
                    }
                }
                return permission;
            }
            else
            {
                if (this.AccessDetails.AccessRules == null) return null;
                string nameExtension = Path.GetExtension(name).ToLower();
                string fileName = Path.GetFileNameWithoutExtension(name);
                //string currentPath = GetPath(location);
                string currentPath = (location + "/");
                foreach (AccessRule fileRule in AccessDetails.AccessRules)
                {
                    if (!string.IsNullOrEmpty(fileRule.Path) && fileRule.IsFile && (fileRule.Role == null || fileRule.Role == AccessDetails.Role))
                    {
                        if (fileRule.Path.IndexOf("*.*") > -1)
                        {
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*.*"));
                            if (currentPath.IndexOf((parentPath)) == 0 || parentPath == "")
                            {
                                permission = UpdateFileRules(permission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf("*.") > -1)
                        {
                            string pathExtension = Path.GetExtension(fileRule.Path).ToLower();
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*."));
                            if (((parentPath) == currentPath || parentPath == "") && nameExtension == pathExtension)
                            {
                                permission = UpdateFileRules(permission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf(".*") > -1)
                        {
                            string pathName = Path.GetFileNameWithoutExtension(fileRule.Path);
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf(pathName + ".*"));
                            if (((parentPath) == currentPath || parentPath == "") && fileName == pathName)
                            {
                                permission = UpdateFileRules(permission, fileRule);
                            }
                        }
                        else if ((fileRule.Path) == (Path.GetFileNameWithoutExtension(location)) || fileRule.Path == location || (fileRule.Path + nameExtension == location))
                        {
                            permission = UpdateFileRules(permission, fileRule);
                        }
                    }
                }
                return permission;
            }

        }

        protected virtual bool HasPermission(Permission rule)
        {
            return rule == Permission.Allow ? true : false;
        }
        protected virtual AccessPermission UpdateFolderRules(AccessPermission folderPermission, AccessRule folderRule)
        {
            folderPermission.Copy = HasPermission(folderRule.Copy);
            folderPermission.Download = HasPermission(folderRule.Download);
            folderPermission.Write = HasPermission(folderRule.Write);
            folderPermission.WriteContents = HasPermission(folderRule.WriteContents);
            folderPermission.Read = HasPermission(folderRule.Read);
            folderPermission.Upload = HasPermission(folderRule.Upload);
            folderPermission.Message = string.IsNullOrEmpty(folderRule.Message) ? string.Empty : folderRule.Message;
            return folderPermission;
        }
        protected virtual AccessPermission UpdateFileRules(AccessPermission filePermission, AccessRule fileRule)
        {
            filePermission.Copy = HasPermission(fileRule.Copy);
            filePermission.Download = HasPermission(fileRule.Download);
            filePermission.Write = HasPermission(fileRule.Write);
            filePermission.Read = HasPermission(fileRule.Read);
            filePermission.Message = string.IsNullOrEmpty(fileRule.Message) ? string.Empty : fileRule.Message;
            return filePermission;
        }

        protected virtual string[] GetFolderDetails(string path)
        {
            string[] str_array = path.Split('/'), fileDetails = new string[2];
            string parentPath = "";
            for (int i = 0; i < str_array.Length - 1; i++)
            {
                parentPath += str_array[i] + "/";
            }
            fileDetails[0] = parentPath;
            fileDetails[1] = str_array[str_array.Length - 1];
            return fileDetails;
        }
    }
}
