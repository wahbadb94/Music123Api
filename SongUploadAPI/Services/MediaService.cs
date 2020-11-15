﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;
using SongUploadAPI.Options;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SongUploadAPI.Utilities;

namespace SongUploadAPI.Services
{
    public class MediaService : IMediaService
    {
        private  IAzureMediaServicesClient _amsClient;
        private readonly MediaServiceSettings _amsSettings;
        private readonly BlobServiceClient _blobServiceClient;

        //NOTE: _amsClient must be initialized asynchronously.
        //      Call Initialize() immediately after object construction!
        public MediaService(IOptions<MediaServiceSettings> amSettings, BlobServiceClient blobServiceClient)
        {
            _amsSettings = amSettings.Value;
            _blobServiceClient = blobServiceClient;
        }

        public async Task Initialize()
        {
            _amsClient = await CreateMediaServicesClientAsync();
        }

        private async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync()
        {
            var credentials = await GetCredentialsAsync();

            return new AzureMediaServicesClient(_amsSettings.ArmEndpoint, credentials)
            {
                SubscriptionId = _amsSettings.SubscriptionId,
            };
        }

        private async Task<ServiceClientCredentials> GetCredentialsAsync()
        {
            // Use ApplicationTokenProvider.LoginSilentAsync to get a token using a service principal with symmetric key
            var clientCredential = new ClientCredential(
                _amsSettings.AadClientId,
                _amsSettings.AadSecret);
            
            return await ApplicationTokenProvider.LoginSilentAsync(
                _amsSettings.AadTenantId,
                clientCredential,
                ActiveDirectoryServiceSettings.Azure);
        }

        //TODO: fileSize is probably redundant because it can be obtained from fileStream
        public async Task<Asset> CreateAndUploadInputAssetAsync(Stream fileStream,
            string assetName,
            string contentType,
            IProgress<long> uploadProgressHandler)
        {
            // creating an 'asset', in the AMS context, means creating
            // a blob storage container.
            Console.WriteLine("Creating input asset/container...");
            var asset = await _amsClient.Assets.CreateOrUpdateAsync(
                _amsSettings.ResourceGroup, _amsSettings.AccountName,
                assetName, new Asset());

            // get a reference to the asset(container) we just created....
            // WARNING: it will not work if you pass in the asset's name!!!
            // AMS's naming method for creating the asset containers is 'asset-<asset.assetid>'
            var assetContainerClient = _blobServiceClient.GetBlobContainerClient($"asset-{asset.AssetId}");
            var blobClient = assetContainerClient.GetBlobClient(assetName);

            Console.WriteLine("Uploading file to blob storage...");
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            await blobClient.UploadAsync(fileStream,
                new BlobHttpHeaders()
                {
                    ContentType = contentType
                },
                progressHandler: uploadProgressHandler);
            stopWatch.Stop();
            Console.WriteLine("File Uploaded to bob storage!");

            return asset;
        }

        public async Task<Asset> CreateOutputAssetAsync(string assetName)
        {
            return await _amsClient.Assets.CreateOrUpdateAsync(_amsSettings.ResourceGroup,
                _amsSettings.AccountName,
                assetName,
                new Asset());
        }

        public async Task<Job> SubmitJobAsync(string inputAssetName, string outputAssetName, string jobName)
        {
            var jobInput = new JobInputAsset(assetName: inputAssetName);

            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(outputAssetName), 
            };

            var job = await _amsClient.Jobs.CreateAsync(
                _amsSettings.ResourceGroup,
                _amsSettings.AccountName,
                _amsSettings.TransformName,
                jobName,
                new Job()
                {
                    Input = jobInput,
                    Outputs = jobOutputs
                });

            return job;
        }

        public async Task<StreamingLocator> CreateStreamingLocatorAsync(string streamingLocatorName, string assetName)
        {
             return await _amsClient.StreamingLocators.CreateAsync(
                _amsSettings.ResourceGroup,
                _amsSettings.AccountName,
                streamingLocatorName,
                new StreamingLocator()
                {
                    AssetName = assetName,
                    StreamingPolicyName = PredefinedStreamingPolicy.ClearStreamingOnly,
                });
        }

        public async Task<IList<string>> GetStreamingUrlsAsync(string locatorName)
        {
            var streamingEndpoint = await _amsClient.StreamingEndpoints.GetAsync(
                _amsSettings.ResourceGroup,
                _amsSettings.AccountName,
                _amsSettings.DefaultStreamingEndpointName);

            if (streamingEndpoint != null)
            {
                if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                {
                    await _amsClient.StreamingEndpoints.StartAsync(
                        _amsSettings.ResourceGroup,
                        _amsSettings.AccountName,
                        _amsSettings.DefaultStreamingEndpointName);
                }
            }

            var paths = await _amsClient.StreamingLocators.ListPathsAsync(
                _amsSettings.ResourceGroup,
                _amsSettings.AccountName,
                locatorName);

            return paths.StreamingPaths.Select(path => new UriBuilder()
            {
                Scheme = "https",
                Host = streamingEndpoint.HostName,
                Path = path.Paths[0]
            }).Select(uriBuilder => uriBuilder.ToString()).ToList();
        }

        private static IProgress<long> GetProgressHandler(long fileSize, Stopwatch stopwatch)
        {
            var builder = new UploadProgressHandlerBuilder(fileSize, stopwatch);
            return builder.Build();
        }
    }
}