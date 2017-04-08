using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System.Web.Script.Serialization;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Text;

namespace Bot_Application1
{

    public class DrugSearchResults
    {
        string brandName;
        string company;
    }

    public class DrugSearchResultsColln
    {
        DrugSearchResults drugDetails;
    }

    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            StringBuilder sb = new StringBuilder();
            if (activity.Type == ActivityTypes.Message)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                // calculate something for us to return
                //int length = (activity.Text ?? string.Empty).Length;

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://drbondsearch.search.windows.net/");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Add("api-key", "9C7CF2B8ED6A48A18B3FE081379045B1");
                    //client.DefaultRequestHeaders.Add("Content-Type","application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    string[] indications = activity.Text.Split(',');
                    StringBuilder sbIndications = new StringBuilder();

                    if (indications.Length > 0) {
                        for(int i=0;i<indications.Length;i++)
                        {
                            if(i!=(indications.Length-1))
                            {
                                sbIndications.Append("indications:" + indications[i]);
                                sbIndications.Append(" and ");
                            }
                            else
                            {
                                sbIndications.Append("indications:" + indications[i]);
                            }
                        }
                    }

                    HttpResponseMessage responseInternal = await client.GetAsync("/indexes/drugtest/docs?api-version=2015-02-28&search=(" + sbIndications.ToString() + ") &queryType=full&searchMode=all&$skip=0&$select=brandName,indications");
                    if (responseInternal.IsSuccessStatusCode)
                    {
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        Stream streamTask = await responseInternal.Content.ReadAsStreamAsync();
                        using (var reader = new StreamReader(streamTask))
                        {

                            var objText = reader.ReadToEnd();
                            Dictionary<string, object> dictionary = serializer.Deserialize<Dictionary<string, object>>(objText);
                            foreach (KeyValuePair<string, object> entry in dictionary)
                            {
                                if (entry.Value is ArrayList)
                                {
                                    foreach (var item in (ICollection)entry.Value)
                                    {
                                        Activity replyToConversation = null;
                                        List<CardImage> cardImages = new List<CardImage>();
                                        cardImages.Add(new CardImage(url: "http://www.e3live.com/App_Themes/Skin_1/CustomImages/capsuleicon.png"));
                                        //List<CardAction> cardButtons = new List<CardAction>();
                                        ThumbnailCard plCard = null;
                                        replyToConversation = activity.CreateReply("");
                                        replyToConversation.TextFormat = TextFormatTypes.Markdown;
                                        replyToConversation.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                                        replyToConversation.Recipient = activity.From;
                                        replyToConversation.Type = "message";

                                        foreach (KeyValuePair<string, object> innerEntry in (Dictionary<string, object>)item)
                                        {
                                            switch (innerEntry.Key)
                                            {
                                                case "@search.score":
                                                    break;
                                                case "brandName":
                                                   
                                                    plCard = new ThumbnailCard()
                                                    {
                                                        Title = innerEntry.Value.ToString(),
                                                        Subtitle = ""
                                                        //Images = cardImages
                                                        //Buttons = cardButtons,
                                                    };
                                                  
                                                    //replyToConversation.Attachments = new List<Attachment>();
                                                    /*CardAction plButton = new CardAction()
                                                    {
                                                        Value = "https://en.wikipedia.org/wiki/Pig_Latin",
                                                        Type = "openUrl",
                                                        Title = "WikiPedia Page"
                                                    };
                                                    cardButtons.Add(plButton);*/

                                                    Attachment plAttachment = plCard.ToAttachment();
                                                    replyToConversation.Attachments.Add(plAttachment);
                                                    sb.Clear();

                                                    break;
                                                case "indications":
                                                    sb.Append(innerEntry.Value);
                                                    break;
                                            }

                                            //Activity replyFinal = activity.CreateReply("Drugs that treat the query " + activity.Text + " are " + sb.ToString());
                                            //await connector.Conversations.ReplyToActivityAsync(replyFinal);

                                            var reply = await connector.Conversations.SendToConversationAsync(replyToConversation);
                                        }
                                    }
                                }
                            }
                            
                        }
                    }
                    else
                    {
                        // return our reply to the user
                        Activity reply = activity.CreateReply("Sorry nothing found");
                        await connector.Conversations.ReplyToActivityAsync(reply);
                    }
                }
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }


        static async Task getBrandForIndications(string indicationsList)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://drbondsearch.search.windows.net/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("api-key", "9C7CF2B8ED6A48A18B3FE081379045B1");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.GetAsync("/indexes('drugtest')/docs/search?api-version=2015-02-28-Preview&search=(indications:" + "fungal" + "infection" + ")&queryType=full&searchMode=all&searchFields=indications");

                Task<Stream> streamTask = response.Content.ReadAsStreamAsync();
                using (var reader = new StreamReader(streamTask.Result))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    var objText = reader.ReadToEnd();
                    //MyObject myojb = (MyObject)js.Deserialize(objText, typeof(MyObject));
                }

                if (response.IsSuccessStatusCode)
                {
                    //var product = await response.Content.ReadAsAsync > Product > ();
                    Console.WriteLine("here");
                    //Console.WriteLine("{0}\t${1}\t{2}", product.Name, product.Price, product.Category);
                }
            }

        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
}