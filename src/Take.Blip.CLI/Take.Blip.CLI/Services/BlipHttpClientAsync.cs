﻿using Lime.Messaging.Resources;
using Lime.Protocol;
using Lime.Protocol.Serialization;
using Lime.Protocol.Serialization.Newtonsoft;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Take.BlipCLI.Handlers;
using Take.BlipCLI.Models;
using Take.BlipCLI.Services.Interfaces;
using Takenet.Iris.Messaging.Resources.ArtificialIntelligence;

namespace Take.BlipCLI.Services
{
    public class BlipHttpClientAsync : IBlipBucketClient, IBlipAIClient, IBlipConfigurationClient
    {
        private string _authorizationKey;
        private readonly IInternalLogger _logger;
        private HttpClient _client = new HttpClient();
        private IEnvelopeSerializer _envelopeSerializer;

        public BlipHttpClientAsync(
            string authorizationKey, 
            IInternalLogger logger,
            IDocumentTypeResolver typeResolver)
        {
            _envelopeSerializer = new EnvelopeSerializer(typeResolver);
            _authorizationKey = authorizationKey;
            _logger = logger;
            _client.BaseAddress = new Uri("https://msging.net");
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Key", authorizationKey);
        }

        public async Task<AnalysisResponse> AnalyseForMetrics(string analysisRequest)
        {
            if (analysisRequest == null) throw new ArgumentNullException(nameof(analysisRequest));

            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri("/analysis"),
                    Method = CommandMethod.Set,
                    Resource = new AnalysisRequest
                    {
                        TestingRequest = true,
                        Text = analysisRequest
                    }
                };

                var envelopeResult = await RunCommandAsync(command);

                return envelopeResult.Resource as AnalysisResponse;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                return null;
            }
        }

        public async Task<bool> PingAsync(string node)
        {
            string validNode = node;

            if (!node.Contains("@"))
            {
                validNode = $"{node}@msging.net";
            }

            var command = new Command
            {
                Id = EnvelopeId.NewId(),
                To = Node.Parse(validNode),
                Uri = new LimeUri("/ping"),
                Method = CommandMethod.Get,
            };

            var envelopeResult = await RunCommandAsync(command);

            return Ping.MediaType.Equals(envelopeResult.Type) && envelopeResult.Status == CommandStatus.Success;
        }

        public async Task AddDocumentAsync(string key, Document document, BucketNamespace bucketNamespace = BucketNamespace.Document)
        {
            try
            {
                string @namespace = GetNamespaceAsString(bucketNamespace);

                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@msging.net"),
                    Uri = new LimeUri($"/{@namespace}/{key}"),
                    Method = CommandMethod.Set,
                    Resource = document
                };

                var envelopeResult = await RunCommandAsync(command);
                EnsureCommandSuccess(envelopeResult);
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
            }
        }

        public async Task<DocumentCollection> GetAllDocumentKeysAsync(BucketNamespace bucketNamespace = BucketNamespace.Document)
        {
            string @namespace = GetNamespaceAsString(bucketNamespace);

            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@msging.net"),
                    Uri = new LimeUri($"/{@namespace}/"),
                    Method = CommandMethod.Get
                };

                var envelopeResult = await RunCommandAsync(command);
                return envelopeResult.Resource as DocumentCollection;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                return null;
            }
        }

        public async Task<IEnumerable<KeyValuePair<string, Document>>> GetAllDocumentsAsync(DocumentCollection keysCollection, BucketNamespace bucketNamespace)
        {
            if (keysCollection.Total == 0) return null;

            try
            {
                string @namespace = GetNamespaceAsString(bucketNamespace);

                var pairsCollection = new List<KeyValuePair<string, Document>>();

                foreach (var key in keysCollection.Items)
                {
                    var command = new Command
                    {
                        Id = EnvelopeId.NewId(),
                        To = Node.Parse("postmaster@msging.net"),
                        Uri = new LimeUri($"/{@namespace}/{key}"),
                        Method = CommandMethod.Get
                    };

                    var envelopeResult = await RunCommandAsync(command);
                    var document = envelopeResult.Resource;

                    pairsCollection.Add(new KeyValuePair<string, Document>(key.ToString(), document));
                }

                return pairsCollection;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                return null;
            }
        }

        public async Task<string> AddEntity(Entity entity)
        {
            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri("/entities"),
                    Method = CommandMethod.Set,
                    Resource = new Entity
                    {
                        Name = entity.Name,
                        Values = entity.Values
                    }
                };
                var envelopeResult = await RunCommandAsync(command);
                EnsureCommandSuccess(envelopeResult);

                var createdEntity = envelopeResult.Resource as Entity;
                return createdEntity.Id;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                return null;
            }
        }

        public async Task DeleteIntent(string intentId)
        {
            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri(Uri.EscapeUriString($"/intentions/{intentId}")),
                    Method = CommandMethod.Delete,
                };

                await RunCommandAsync(command);
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
            }
        }

        public async Task DeleteEntity(string entityId)
        {
            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri(Uri.EscapeUriString($"/entities/{entityId}")),
                    Method = CommandMethod.Delete,
                };

                await RunCommandAsync(command);
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
            }
        }

        public async Task<string> AddIntent(string intentName, bool verbose = false)
        {
            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri("/intentions"),
                    Method = CommandMethod.Set,
                    Resource = new Intention
                    {
                        Name = intentName,
                    }
                };

                var envelopeResult = await RunCommandAsync(command);
                if (envelopeResult.Status == CommandStatus.Success)
                {
                    var createdIntention = envelopeResult.Resource as Intention;
                    return createdIntention.Id;
                }

                LogVerboseLine($"{intentName}: {envelopeResult.Status} - {envelopeResult.Reason.Code} = {envelopeResult.Reason.Description}");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message: {0} ", e.Message);
                return null;
            }
        }

        public async Task AddQuestions(string intentId, Question[] questions)
        {
            if (questions == null) throw new ArgumentNullException(nameof(questions));

            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri(Uri.EscapeUriString($"/intentions/{intentId}/questions")),
                    Method = CommandMethod.Set,
                    Resource = new DocumentCollection
                    {
                        ItemType = Question.MediaType,
                        Items = questions
                    }
                };

                var envelopeResult = await RunCommandAsync(command);
                EnsureCommandSuccess(envelopeResult);
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                throw;
            }
        }

        public async Task AddAnswers(string intentId, Answer[] answers)
        {
            if (answers == null) throw new ArgumentNullException(nameof(answers));

            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri(Uri.EscapeUriString($"/intentions/{intentId}/answers")),
                    Method = CommandMethod.Set,
                    Resource = new DocumentCollection
                    {
                        ItemType = Answer.MediaType,
                        Items = answers
                    }
                };

                var envelopeResult = await RunCommandAsync(command);
                EnsureCommandSuccess(envelopeResult);
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                throw;
            }
        }

        public async Task<List<Entity>> GetAllEntities(bool verbose = false)
        {
            var entitiesList = new List<Entity>();

            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri($"/entities"),
                    Method = CommandMethod.Get,
                };

                _logger.LogDebug("Start get entities");

                var envelopeResult = await RunCommandAsync(command);
                var entities = envelopeResult.Resource as DocumentCollection ?? new DocumentCollection { Items = Enumerable.Empty<Document>().ToArray() };

                var counter = 0;
                var totalEntities = entities.Total;
                var str = BuildProcessBarLog(totalEntities, counter);
                _logger.LogTrace(str.ToString());
                foreach (var entity in entities)
                {
                    entitiesList.Add(entity as Entity);
                    counter++;
                    str = BuildProcessBarLog(totalEntities, counter);
                    _logger.LogTrace(str.ToString());
                }
                _logger.LogDebug("Finish get entities");

                return entitiesList;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                return null;
            }
        }

        public async Task<List<Intention>> GetAllIntentsAsync(bool verbose = false, bool justIds = false)
        {
            var intentsList = new List<Intention>();

            try
            {
                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Uri = new LimeUri($"/intentions?$take=500"),
                    Method = CommandMethod.Get,
                };

                var commandBase = new Command
                {
                    To = Node.Parse("postmaster@ai.msging.net"),
                    Method = CommandMethod.Get,
                };

                _logger.LogDebug("Start get intents");

                var envelopeResult = await RunCommandAsync(command);
                var intents = envelopeResult.Resource as DocumentCollection ?? new DocumentCollection { Items = Enumerable.Empty<Document>().ToArray() };

                var totalIntents = intents.Total;
                var counter = 0;
                var str = BuildProcessBarLog(totalIntents, counter);
                _logger.LogTrace(str.ToString());
                foreach (var intent in intents)
                {
                    counter++;
                    var intention = intent as Intention;

                    if (!justIds)
                    {
                        //Answers
                        var uri = Uri.EscapeUriString($"/intentions/{intention.Id}/answers");
                        commandBase.Uri = new LimeUri(uri);
                        envelopeResult = await GetCommandResultAsync(commandBase);
                        if (envelopeResult.Status != CommandStatus.Failure)
                        {
                            var answers = envelopeResult.Resource as DocumentCollection;
                            intention.Answers = answers.Items.Select(i => i as Answer).ToArray();
                        }

                        //Questions
                        uri = Uri.EscapeUriString($"/intentions/{intention.Id}/questions?$take=5000");
                        commandBase.Uri = new LimeUri(uri);
                        envelopeResult = await GetCommandResultAsync(commandBase);
                        if (envelopeResult.Status != CommandStatus.Failure)
                        {
                            var questions = envelopeResult.Resource as DocumentCollection;
                            intention.Questions = questions.Items.Select(i => i as Question).ToArray();
                        }
                    }

                    intentsList.Add(intention);

                    str = BuildProcessBarLog(totalIntents, counter);
                    _logger.LogTrace(str.ToString());
                }

                _logger.LogDebug("Finish get intents");

                return intentsList;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                return null;
            }
        }

       
        public async Task<KeyValuePair<string, Document>?> GetDocumentAsync(string key, BucketNamespace bucketNamespace)
        {
            try
            {
                string @namespace = GetNamespaceAsString(bucketNamespace);

                var pairsCollection = new List<KeyValuePair<string, Document>>();

                var command = new Command
                {
                    Id = EnvelopeId.NewId(),
                    To = Node.Parse("postmaster@msging.net"),
                    Uri = new LimeUri($"/{@namespace}/{key}"),
                    Method = CommandMethod.Get
                };

                var envelopeResult = await RunCommandAsync(command);
                var document = envelopeResult.Resource;

                var value = new KeyValuePair<string, Document>(key.ToString(), document);

                return value;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "\nException Caught!");
                _logger.LogError(e, "Message :{0} ", e.Message);
                return null;
            }
        }

        public async Task<string> GetMessengerQRCodeAsync(string node, bool verbose = false, string payload = "", bool download = false)
        {
            // BlipCommand to get advanced configurations
            var blipCommand = new Command
            {
                To = Node.Parse("postmaster@msging.net"),
                Id = EnvelopeId.NewId(),
                Uri = new LimeUri($"lime://{node}@msging.net/configuration/caller"),
                Method = CommandMethod.Get
            };

            var blipResponse = await RunCommandAsync(blipCommand);
            var items = ((blipResponse.Resource as DocumentCollection).Items);
            var pageAccessToken = string.Empty;

            // Finds PageAccessToken from array
            foreach (var i in items)
            {
                var item = (i as CallerResource);
                if (item.Name.Equals("PageAccessToken"))
                {
                    pageAccessToken = item.Value;
                    break;
                }
            }
            if (pageAccessToken.IsNullOrEmpty())
            {
                throw new Exception("Bot has no Facebook Messenger Access Token.");
            }

            LogVerboseLine($"\tPageAccessToken:\t{pageAccessToken}");

            // Get QR Code from Facebook API
            var client = new RestClient($"https://graph.facebook.com/v2.6/me/messenger_codes?access_token={pageAccessToken}");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            var body = "{\r\n  \"type\": \"standard\",\r\n  \"data\": {\r\n    \"ref\":\"" + payload + "\"\r\n  },\r\n  \"image_size\": 1000\r\n}";
            request.AddParameter("undefined", $"{body}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            var qrCode = JsonConvert.DeserializeObject<FacebookQrCodeResponse>(response.Content);
            LogVerboseLine($"\tQR Code URL:\t{qrCode.Uri}");

            if (download)
            {
                WebClient webClient = new WebClient();
                webClient.DownloadFile(qrCode.Uri, "qr.png");
                LogVerboseLine("Successfully saved file \"qr.png\" locally.");
            }

            return qrCode.Uri;
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private async Task<Command> GetCommandResultAsync(Command command)
        {
            command.Id = EnvelopeId.NewId();
            return await RunCommandAsync(command);
        }

        private async Task<Command> RunCommandAsync(Command command)
        {
            var commandString = _envelopeSerializer.Serialize(command);

            using (var httpContent = new StringContent(commandString, Encoding.UTF8, "application/json"))
            {

                try
                {
                    var response = await _client.PostAsync("/commands", httpContent);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();
                    return (Command)_envelopeSerializer.Deserialize(responseBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to run command");
                    throw ex;
                }
            }
        }

        private void LogVerbose(string message)
        {
            _logger.LogDebug(message);
        }

        private void LogVerboseLine(string message)
        {
            _logger.LogDebug(message + "\n");
        }

        private void EnsureCommandSuccess(Command envelopeResult)
        {
            if (envelopeResult.Status == CommandStatus.Failure)
                throw new Exception($"Command failed: {envelopeResult.Reason.Description}({envelopeResult.Reason.Code})");
        }

        private static string GetNamespaceAsString(BucketNamespace bucketNamespace)
        {
            string @namespace;
            switch (bucketNamespace)
            {
                case BucketNamespace.Document:
                    @namespace = "buckets";
                    break;

                case BucketNamespace.Resource:
                    @namespace = "resources";
                    break;

                case BucketNamespace.Profile:
                    @namespace = "profile";
                    break;
                default:
                    @namespace = "buckets";
                    break;
            }

            return @namespace;
        }

        private static StringBuilder BuildProcessBarLog(int totalIntents, int counter)
        {
            var str = new StringBuilder($"{counter}/{totalIntents}: ");
            for (int j = 0; j < counter; j++)
            {
                str.Append("*");
            }
            for (int j = counter; j < totalIntents; j++)
            {
                str.Append("_");
            }
            str.Append("|");
            return str;
        }

    }

}




