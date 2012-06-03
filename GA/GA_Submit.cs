/// <summary>
/// This class handles sending data to the Game Analytics servers.
/// JSON data is sent using a MD5 hashed authorization header, containing the JSON data and private key
/// </summary>

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System;
using LitJson;

public static class GA_Submit
{	
	/// <summary>
	/// Handlers for success and fail during submit to the GA server
	/// </summary>
	public delegate void SubmitSuccessHandler(List<Item> items, bool success);
	public delegate void SubmitErrorHandler(List<Item> items);
	
	/// <summary>
	/// Types of services on the GA server
	/// </summary>
	public enum CategoryType { GA_User, GA_Event, GA_Log, GA_Purchase }
	
	/// <summary>
	/// An item is a message (parameters) and the category (GA service) the message should be sent to
	/// </summary>
	public struct Item
	{
		public CategoryType Type;
		public Dictionary<string, object> Parameters;
		public float AddTime;
	}
	
	#region private values
	
	/// <summary>
	/// All the different types of GA services
	/// </summary>
	public static Dictionary<CategoryType, string> Categories = new Dictionary<CategoryType, string>()
	{
		{ CategoryType.GA_User, "user" },
		{ CategoryType.GA_Event, "design" },
		{ CategoryType.GA_Log, "quality" },
		{ CategoryType.GA_Purchase, "business" }
	};
	
	private static string _publicKey;
	private static string _privateKey;
	private static string _baseURL = "http://api.gameanalytics.com";
	private static string _version = "1";
	
	#endregion
	
	#region public methods
	
	/// <summary>
	/// Sets the users public and private keys for the GA server
	/// </summary>
	/// <param name="publicKey">
	/// The public key which identifies this users game <see cref="System.String"/>
	/// </param>
	/// <param name="privateKey">
	/// The private key used to encode messages <see cref="System.String"/>
	/// </param>
	public static void SetupKeys(string publicKey, string privateKey)
	{
		_publicKey = publicKey;
		_privateKey = privateKey;
	}
	
	/// <summary>
	/// Devides a list of messages into categories. All items in each category are submitted together to the GA server
	/// </summary>
	/// <param name="queue">
	/// The list of items holding message and service type <see cref="List<Item>"/>
	/// </param>
	/// <param name="successEvent">
	/// Each of the successful messages will call this <see cref="SubmitSuccessHandler"/>
	/// </param>
	/// <param name="errorEvent">
	/// Each of the failed (an error occurs during submit) messages will call this <see cref="SubmitErrorHandler"/>
	/// </param>
	public static void SubmitQueue(List<Item> queue, SubmitSuccessHandler successEvent, SubmitErrorHandler errorEvent)
	{			
		if (_publicKey.Equals("") || _privateKey.Equals(""))
		{
			Debug.LogError("GA Error: Public key and/or private key not set. Use GASubmit.SetupKeys(publicKey, privateKey) to set keys.");
			return;
		}
		
		Dictionary<CategoryType, List<Item>> categories = new Dictionary<CategoryType, List<Item>>();
		
		/* Put all the items in the queue into a list containing only the messages of that category type.
		 * This way we end up with a list of items for each category type */
		foreach (Item item in queue)
		{
			if (categories.ContainsKey(item.Type))
			{
				/* If we already added another item of this type then remove the UserID, SessionID, and Build values if necessary.
				 * These values only need to be present in each message once, since they will be the same for all items */
				
				// TODO: below not supported yet in API - activate once redundant data can be trimmed
				/*
				if (item.Parameters.ContainsKey(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.UserID]))
					item.Parameters.Remove(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.UserID]);
				
				if (item.Parameters.ContainsKey(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.SessionID]))
					item.Parameters.Remove(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.SessionID]);
				
				if (item.Parameters.ContainsKey(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Build]))
					item.Parameters.Remove(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Build]);
				*/
				categories[item.Type].Add(item);
			}
			else
			{
				/* If we did not add another item of this type yet, then add the UserID, SessionID, and Build values if necessary.
				 * These values only need to be present in each message once, since they will be the same for all items */
				
				if (!item.Parameters.ContainsKey(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.UserID]))
					item.Parameters.Add(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.UserID], GA_GenericInfo.UserID);
				
				if (!item.Parameters.ContainsKey(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.SessionID]))
					item.Parameters.Add(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.SessionID], GA_GenericInfo.SessionID);
				
				if (!item.Parameters.ContainsKey(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Build]))
					item.Parameters.Add(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Build], GA.BUILD);
				
				categories.Add(item.Type, new List<Item> { item });
			}
		}
		
		//For each existing category, submit a message containing all the items of that category type
		foreach (KeyValuePair<CategoryType, List<Item>> kvp in categories)
		{
			GA.RunCoroutine(Submit(kvp.Value, successEvent, errorEvent));
		}
	}
	
	/// <summary>
	/// Submits a list of messages (which must all be of the same type) to the GA server
	/// </summary>
	/// <param name="item">
	/// The list of items, each holding a message and service type <see cref="Item"/>
	/// </param>
	/// <param name="successEvent">
	/// If successful this will be fired <see cref="SubmitSuccessHandler"/>
	/// </param>
	/// <param name="errorEvent">
	/// If an error occurs this will be fired <see cref="SubmitErrorHandler"/>
	/// </param>
	/// <returns>
	/// A <see cref="IEnumerator"/>
	/// </returns>
	public static IEnumerator Submit(List<Item> items, SubmitSuccessHandler successEvent, SubmitErrorHandler errorEvent)
	{
		if (items.Count == 0)
		{
			yield break;
		}
		
		//Since all the items must have the same category (we make sure they do below) we can get the category from the first item
		CategoryType serviceType = items[0].Type;
		string url = GetURL(serviceType);
		
		//Make sure that all items are of the same category type, and put all the parameter collections into a list
		List<Dictionary<string, object>> itemsParameters = new List<Dictionary<string, object>>();
		
		for (int i = 0; i < items.Count; i++)
		{
			if (serviceType != items[i].Type)
			{
				Debug.LogError("GA Error: All messages in a submit must be of the same service/category type.");
				if (errorEvent != null)
				{
					errorEvent(items);
				}
				yield break;
			}
			
			Dictionary<string, object> parameters = items[i].Parameters;
			itemsParameters.Add(parameters);
		}
		
		//Make a JSON array string out of the list of parameter collections
		string json = JsonMapper.ToJson(itemsParameters);
		
		/* Mobile devices: If we do not have access to a network connection (or we are roaming and roaming is disabled),
		 * and data is set to be archived, then archive the data and pretend the message was sent successfully */
		if  (GA.ARCHIVEDATA &&
			(Application.internetReachability == NetworkReachability.NotReachable || 
			(Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork && !GA.ALLOWROAMING)))
		{
			GA_Archive.ArchiveData(json, serviceType);
			if (successEvent != null)
			{
				successEvent(items, true);
			}
			yield break;
		}
		
		//Prepare the JSON array string for sending by converting it to a byte array
		byte[] data = Encoding.ASCII.GetBytes(json);
		
		//Set the authorization header to contain an MD5 hash of the JSON array string + the private key
		Hashtable headers = new Hashtable();
		headers.Add("Authorization", CreateMD5Hash(json));
		
		//Try to send the data
		WWW www = new WWW(url, data, headers);
		
		//Wait for response
		yield return www;
		
		if (GA.DEBUG)
		{
			Debug.Log("GA URL: " + url);
			Debug.Log("GA Request: " + json);
			Debug.Log("GA Hash: " + CreateMD5Hash(json));
		}
		
		try
		{
			if (www.error != null)
			{
				throw new Exception(www.error);
			}
			
			//Get the JSON object from the response
			Dictionary<string, object> returnParam = JsonMapper.ToObject<Dictionary<string, object>>(www.text);
			
			//If the response contains the key "status" with the value "ok" we know that the message was sent and recieved successfully
			if (returnParam != null &&
			    returnParam.ContainsKey("status") && returnParam["status"].ToString().Equals("ok"))
			{
				if (GA.DEBUG)
				{
					Debug.Log("GA Result: " + www.text);
				}
				
				if (successEvent != null)
				{
					successEvent(items, true);
				}
			}
			else
			{
				/* The message was not sent and recieved successfully: Stop submitting all together if something
				 * is completely wrong and we know we will not be able to submit any messages at all..
				 * Such as missing or invalid public and/or private keys */
				if (returnParam != null &&
				    returnParam.ContainsKey("message") && returnParam["message"].ToString().Equals("Game not found") &&
					returnParam.ContainsKey("code") && returnParam["code"].ToString().Equals("400"))
				{
					//If it was not a QA message, or we are not re-submitting errors, we give an error, otherwise just a warning (errors will be picked up and re-submitted automatically, and we don't want to loop that behavior)
					if (serviceType != CategoryType.GA_Log || !GA.SUBMITERRORS)
						Debug.LogError("GA Error: " + www.text + " (NOTE: make sure your public and private keys match the keys you recieved from the Game Analytics website. It might take a few minutes before a newly added game will be able to recieve data.)");
					else
						Debug.LogWarning("GA Error: " + www.text + " (NOTE: make sure your public and private keys match the keys you recieved from the Game Analytics website. It might take a few minutes before a newly added game will be able to recieve data.)");
					
					//An error event with a null parameter will stop the GA wrapper from submitting messages
					if (errorEvent != null)
					{
						errorEvent(null);
					}
				}
				else
				{
					//If it was not a QA message, or we are not re-submitting errors, we give an error, otherwise just a warning (errors will be picked up and re-submitted automatically, and we don't want to loop that behavior)
					if (serviceType != CategoryType.GA_Log || !GA.SUBMITERRORS)
						Debug.LogError("GA Error: " + www.text);
					else
						Debug.LogWarning("GA Error: " + www.text);
					
					if (errorEvent != null)
					{
						errorEvent(items);
					}
				}
			}
		}
		catch (Exception e)
		{
			//If it was not a QA message, or we are not re-submitting errors, we give an error, otherwise just a warning (errors will be picked up and re-submitted automatically, and we don't want to loop that behavior)
			if (serviceType != CategoryType.GA_Log || !GA.SUBMITERRORS)
				Debug.LogError("GA Error: " + e.Message);
			else
				Debug.LogWarning("GA Error: " + e.Message);
			
			/* If we hit one of these errors we should not attempt to send the message again
			 * (if necessary we already threw a GA Error which may be tracked) */
			if (e.Message.Contains("400 Bad Request"))
			{
				//An error event with a null parameter will stop the GA wrapper from submitting messages
				if (successEvent != null)
				{
					successEvent(items, false);
				}
			}
			else
			{
				if (errorEvent != null)
				{
					errorEvent(items);
				}
			}
		}
	}
	
	/// <summary>
	/// Gets the base url to the GA server
	/// </summary>
	/// <param name="inclVersion">
	/// Should the version be included? <see cref="System.Boolean"/>
	/// </param>
	/// <returns>
	/// A string representing the base url (+ version if inclVersion is true) <see cref="System.String"/>
	/// </returns>
	public static string GetBaseURL(bool inclVersion)
	{
		if (inclVersion)
			return _baseURL + "/" + _version;
		
		return _baseURL;
	}
	
	#endregion
	
	#region private methods
	
	/// <summary>
	/// Gets the url on the GA server matching the specific service we are interested in
	/// </summary>
	/// <param name="category">
	/// Determines the GA service/category <see cref="CategoryType"/>
	/// </param>
	/// <returns>
	/// A string representing the url matching our service choice on the GA server <see cref="System.String"/>
	/// </returns>
	private static string GetURL(CategoryType category)
	{
		return _baseURL + "/" + _version + "/" + _publicKey + "/" + Categories[category];
	}
	
	/// <summary>
	/// Encodes the input (json message) and the users private key as a MD5 hash
	/// </summary>
	/// <param name="input">
	/// The input message we want encoded <see cref="System.String"/>
	/// </param>
	/// <returns>
	/// The MD5 hash encoded result of input + private key <see cref="System.String"/>
	/// </returns>
	private static string CreateMD5Hash(string input)
	{
		/* This was used when the MD5 hash had to be converted to Base64, etc.
		MD5 md5Hash = MD5.Create();
		byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input + _privateKey));
		return BitConverter.ToString(data); //System.Convert.ToBase64String(data);
		*/
		
		// Gets the MD5 hash for input and _privateKey
		MD5 md5 = new MD5CryptoServiceProvider();
		byte[] data = Encoding.Default.GetBytes(input + _privateKey);
		byte[] hash = md5.ComputeHash(data);
		// Transforms as hexa
		string hexaHash = "";
		foreach (byte b in hash) {
			hexaHash += String.Format("{0:x2}", b);
		}
		// Returns MD5 hexa hash
		return hexaHash;
	}
	
	#endregion
}