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
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow.Advanced;

namespace Bot_Application1
{
    public static class StringBuilderExtensions
    {
        public static void AppendLineWithTwoWhiteSpacePrefix(this StringBuilder sb, string value)
        {
            sb.AppendFormat("{0}{1}{2}", "  ", value, Environment.NewLine);
        }

        public static void AppendLineWithTwoWhiteSpacePrefix(this StringBuilder sb)
        {
            sb.AppendFormat("{0}{1}", "  ", Environment.NewLine);
        }
    }

    [Serializable]
    public class EchoDialog : IDialog<object>
    {
        protected int count = 1;
        protected Dictionary<string, string> dicDrug;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="argument"></param>
        /// <returns></returns>
        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var message = await argument;

            if (message.Text.ToLower().Contains("info"))
            {
                string drugToFurtherGet = string.Empty;
                dicDrug.TryGetValue(message.Text.ToLower().Replace("info", "").Trim(), out drugToFurtherGet);

                if (drugToFurtherGet != null)
                {
                    //Not nuller - so user typed some info that we have (may be till 5)
                    StringBuilder sb = new StringBuilder();

                    using (var client = new HttpClient())
                    {
                        client.BaseAddress = new Uri("https://drbondsearch.search.windows.net/");
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Add("api-key", "9C7CF2B8ED6A48A18B3FE081379045B1");
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        string[] drugName = drugToFurtherGet.Split(',');
                        StringBuilder sbIndications = new StringBuilder();

                        if (drugName.Length > 0)
                        {
                            for (int i = 0; i < drugName.Length; i++)
                            {
                                if (i != (drugName.Length - 1))
                                {
                                    sbIndications.Append("brandName:" + drugName[i]);
                                    sbIndications.Append(" and ");
                                }
                                else
                                {
                                    sbIndications.Append("brandName:" + drugName[i]);
                                }
                            }
                        }

                        HttpResponseMessage responseInternal = await client.GetAsync("/indexes/drugtest/docs?api-version=2015-02-28&search=(" + sbIndications.ToString() + ") &queryType=full&searchMode=all&$select=brandName,company,mrp,compositionStrength,presentation,pack,indications");
                        if (responseInternal.IsSuccessStatusCode)
                        {
                            JavaScriptSerializer serializer = new JavaScriptSerializer();
                            Stream streamTask = await responseInternal.Content.ReadAsStreamAsync();
                            int drugCounter = 1;

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
                                            string strBrandName = string.Empty;
                                            string strCompName = string.Empty;
                                            Dictionary<string, object> dt = (Dictionary<string, object>)item;

                                            //brandName,company,mrp,compositionStrength,presentation,pack

                                            sb.Append(dt["brandName"].ToString());
                                            sb.Append("   " + dt["presentation"].ToString());
                                            sb.Append(" (" + dt["pack"].ToString() + ")");
                                            sb.Append("  MRP-" + dt["mrp"].ToString());
                                            sb.AppendLineWithTwoWhiteSpacePrefix();
                                            sb.Append("(" + dt["company"].ToString() + ")");
                                            sb.AppendLineWithTwoWhiteSpacePrefix();
                                            sb.Append("Generic strength :");
                                            sb.AppendLineWithTwoWhiteSpacePrefix();
                                            sb.AppendLine(dt["compositionStrength"].ToString());
                                            sb.AppendLineWithTwoWhiteSpacePrefix();
                                            sb.Append("Indications :");
                                            sb.AppendLineWithTwoWhiteSpacePrefix();
                                            sb.AppendLine(dt["indications"].ToString());
                                            sb.AppendLineWithTwoWhiteSpacePrefix();
                                            sb.AppendLineWithTwoWhiteSpacePrefix();
                                            sb.AppendLine(" --------------------------------------");
                                        }
                                    }
                                }
                            }
                        }
                        ///hello e

                        //Ask the user to select a value for getting more details on the drug
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                        sb.AppendLine("Please type 'info' followed by the name of the drug to get more details of the drug. For example 'info 1'. If you want a new query type 'reset'");
                        sb.AppendLineWithTwoWhiteSpacePrefix();

                        dicDrug = dicDrug;

                        await context.PostAsync(sb.ToString());
                        context.Wait(MessageReceivedAsync);

                    }
                }
                else
                {
                    //No drugs found - use typed some vague info x (so eject)
                    await context.PostAsync("Sorry, could not find the details of the drug that you typed. Kindly type from the listing of drugs with 'info' followed by drug number.");
                    context.Wait(MessageReceivedAsync);
                }
            }
            else if (message.Text.ToLower() == "reset")
            {
                PromptDialog.Confirm(
                    context,
                    AfterResetAsync,
                    "Are you sure you want to search for a new indication?",
                    "Didn't get that!",
                    promptStyle: PromptStyle.None);
            }
            else
            {

                StringBuilder sb = new StringBuilder();
                dicDrug = new Dictionary<string, string>();

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://drbondsearch.search.windows.net/");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Add("api-key", "9C7CF2B8ED6A48A18B3FE081379045B1");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    string[] indications = message.Text.Split(',');
                    StringBuilder sbIndications = new StringBuilder();

                    if (indications.Length > 0)
                    {
                        for (int i = 0; i < indications.Length; i++)
                        {
                            if (i != (indications.Length - 1))
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

                    HttpResponseMessage responseInternal = await client.GetAsync("/indexes/drugtest/docs?api-version=2015-02-28&search=(" + sbIndications.ToString() + ") &queryType=full&searchMode=all&$top=5&$skip=5&$select=brandName,company,id");
                    if (responseInternal.IsSuccessStatusCode)
                    {
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        Stream streamTask = await responseInternal.Content.ReadAsStreamAsync();
                        int drugCounter = 1;
                        int tempDrugCounter = 0;

                        using (var reader = new StreamReader(streamTask))
                        {

                            var objText = reader.ReadToEnd();
                            Dictionary<string, object> dictionary = serializer.Deserialize<Dictionary<string, object>>(objText);

                            foreach (KeyValuePair<string, object> entry in dictionary)
                            {
                                if (entry.Value is ArrayList && ((System.Collections.ArrayList)entry.Value).Count > 0)
                                {
                                    sb.Append(" Drbond interaction checker bot has identified top 5 drug brands that are possibly related to the indication that you queried.");
                                    sb.AppendLineWithTwoWhiteSpacePrefix();
                                    sb.AppendLineWithTwoWhiteSpacePrefix();

                                    foreach (var item in (ICollection)entry.Value)
                                    {
                                        string strBrandName = string.Empty;
                                        string strCompName = string.Empty;
                                        Dictionary<string, object> dt = (Dictionary<string, object>)item;

                                        sb.Append(drugCounter + " . " + dt["brandName"].ToString());
                                        sb.Append(" (" + dt["company"].ToString() + ") ");
                                        sb.AppendLineWithTwoWhiteSpacePrefix();

                                        dicDrug.Add(drugCounter.ToString(), dt["brandName"].ToString());
                                        drugCounter++;
                                    }

                                }
                            }
                        }
                    }

                    if (sb.ToString().Length > 0)
                    {
                        //Ask the user to select a value for getting more details on the drug
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                        sb.AppendLine("Please type 'info' followed by the number against the drug to get more details of the drug. For example 'info 1'. If you want a new query type 'reset'");
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                    }
                    else
                    {
                        //Ask the user to select a value for getting more details on the drug
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                        sb.AppendLine("Sorry, we could not find any drugs that match to the searched indication. Please try a different indication");
                        sb.AppendLineWithTwoWhiteSpacePrefix();
                    }

                    await context.PostAsync(sb.ToString());
                    context.Wait(MessageReceivedAsync);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="argument"></param>
        /// <returns></returns>
        public async Task AfterResetAsync(IDialogContext context, IAwaitable<bool> argument)
        {
            var confirm = await argument;
            if (confirm)
            {
                this.count = 1;
                await context.PostAsync("Your indication search is reset. Please type a new indication.");
            }
            else
            {
                await context.PostAsync("Please continue typeing info followed by drug number to get more details");
            }
            context.Wait(MessageReceivedAsync);
        }

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
            if(activity.GetActivityType() == ActivityTypes.ContactRelationUpdate)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                Activity reply = activity.CreateReply("Hello, Welcome to DrBond indicator bot. DrBond Health interactive indication bot lets you check out medical indications to find common causes, a possible diagnosis, and brands that can treat the indication. Start typing your indications. Eg - asthma,bronchospasm");
                await connector.Conversations.ReplyToActivityAsync(reply);
            }
            if(activity.GetActivityType() == ActivityTypes.DeleteUserData)
            {

            }
            // check if activity is of type message
            if (activity != null && activity.GetActivityType() == ActivityTypes.Message)
            {
                //Now the actual query
                await Conversation.SendAsync(activity, () => new EchoDialog());
            }
            else
            {
                HandleSystemMessage(activity);
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);

            /*StringBuilder sb = new StringBuilder();
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

                    HttpResponseMessage responseInternal = await client.GetAsync("/indexes/drugtest/docs?api-version=2015-02-28&search=(" + sbIndications.ToString() + ") &queryType=full&searchMode=all&$top=5&$skip=5&$select=brandName,company");
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
                                        string strBrandName = string.Empty;
                                        string strCompName = string.Empty;
                                        foreach (KeyValuePair<string, object> innerEntry in (Dictionary<string, object>)item)
                                        {
                                            switch (innerEntry.Key)
                                            {
                                                case "@search.score":
                                                    break;
                                                case "brandName":
                                                    strBrandName = innerEntry.Value.ToString();
                                                    break;
                                                case "company":
                                                    strCompName = " (" + innerEntry.Value + ") ";
                                                    break;
                                            }
                                        }
                                        Activity replyFinal = activity.CreateReply(strBrandName + strCompName);
                                        await connector.Conversations.ReplyToActivityAsync(replyFinal);
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
            return response;*/
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


/*
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

      /// <summary>
        /// 
        /// </summary>
        /// <param name="indicationsFromUser"></param>
        /// <returns></returns>
        public async Task<string> getDrugsForIndication(string indicationsFromUser)
        {
            StringBuilder sb = new StringBuilder();

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://drbondsearch.search.windows.net/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("api-key", "9C7CF2B8ED6A48A18B3FE081379045B1");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string[] indications = indicationsFromUser.Split(',');
                StringBuilder sbIndications = new StringBuilder();

                if (indications.Length > 0)
                {
                    for (int i = 0; i < indications.Length; i++)
                    {
                        if (i != (indications.Length - 1))
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

                HttpResponseMessage responseInternal = await client.GetAsync("/indexes/drugtest/docs?api-version=2015-02-28&search=(" + sbIndications.ToString() + ") &queryType=full&searchMode=all&$top=5&$skip=5&$select=brandName,company");
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
                                    string strBrandName = string.Empty;
                                    string strCompName = string.Empty;
                                    foreach (KeyValuePair<string, object> innerEntry in (Dictionary<string, object>)item)
                                    {
                                        switch (innerEntry.Key)
                                        {
                                            case "@search.score":
                                                break;
                                            case "brandName":
                                                sb.Append(innerEntry.Value.ToString());
                                                break;
                                            case "company":
                                                sb.Append(" (" + innerEntry.Value + ") ");
                                                break;
                                        }
                                    }
                                }

                            }
                        }
                        return sb.ToString();
                    }
                }
                else
                {
                    // return our reply to the user
                    return sb.ToString();
                }
            }
        }

    foreach (KeyValuePair<string, object> innerEntry in (Dictionary<string, object>)item)
                                        {
                                            //sb.Append(item[])

                                            switch (innerEntry.Key)
                                            {
                                                case "@search.score":
                                                    break;
                                                case "brandName":
                                                    sb.Append(drugCounter + "." + innerEntry.Value.ToString());
                                                    strBrandName = innerEntry.Value.ToString();
                                                    tempDrugCounter = drugCounter;
                                                    drugCounter++;
                                                    sb.AppendLine();
                                                    break;
                                                case "company":
                                                    strCompName = innerEntry.Value.ToString();
                                                    sb.Append(" (" + innerEntry.Value + ") ");
                                                    break;
                                            }
                                            if (innerEntry.Key != "@search.score" && strCompName != string.Empty)
                                            {
                                                dicDrug.Add(tempDrugCounter.ToString(), strBrandName);
                                                strCompName = string.Empty;
                                            } 
                                        }
    //await context.PostAsync(string.Format(" You are looking for more details on " + drugToFurtherGet));
                //context.Wait(MessageReceivedAsync);
*/
