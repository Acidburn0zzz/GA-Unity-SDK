/// <summary>
/// This class handles archiving of data when internet connection is not available. The priority for archiving data follows the
/// categories: user > business > quality > design
/// </summary>

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using LitJson;

public static class GA_Archive
{
	public static string FILE_NAME = "GA_archive";
	
	/// <summary>
	/// Archives json data so it can be sent at a later time, when an internet connection is available.
	/// </summary>
	/// <param name='json'>
	/// The json data as a string
	/// </param>
	/// <param name='serviceType'>
	/// The category type
	/// </param>
	public static void ArchiveData(string json, GA_Submit.CategoryType serviceType)
	{
		StreamWriter fileWriter = null;
		string fileName = Application.persistentDataPath + "/" + FILE_NAME;
		
		if (File.Exists(fileName))
		{
			fileWriter = File.AppendText(fileName);
		}
		else
		{
			fileWriter = File.CreateText(fileName);
		}
		
		if (fileWriter != null)
		{
			fileWriter.WriteLine(serviceType + " " + json);
			fileWriter.Close();
		}
	}
	
	/// <summary>
	/// Gets data which has previously been archived due to lack of internet connectivity.
	/// The file containing the archived data is then deleted.
	/// </summary>
	/// <returns>
	/// The archived data as a list of items with parameters and category
	/// </returns>
	public static List<GA_Submit.Item> GetArchivedData()
	{
		List<GA_Submit.Item> items = new List<GA_Submit.Item>();
		
		StreamReader fileReader = null;
		string fileName = Application.persistentDataPath + "/" + FILE_NAME;
		
		if (File.Exists(fileName))
		{
			fileReader = File.OpenText(fileName);
		}
		
		if (fileReader != null)
		{
			string line = null;
			while ((line = fileReader.ReadLine()) != null)
			{
				string[] lineSplit = line.Split(' ');
				if (lineSplit.Length >= 2)
				{
					string categoryString = lineSplit[0];
					string json = line.Substring(lineSplit[0].Length + 1);
					
					bool saveData = false;
					GA_Submit.CategoryType category = GA_Submit.CategoryType.GA_User;
					
					foreach (KeyValuePair<GA_Submit.CategoryType, string> kvp in GA_Submit.Categories)
					{
						if (kvp.Key.ToString().Equals(categoryString))
						{
							category = kvp.Key;
							saveData = true;
						}
					}
					
					if (saveData)
					{
						List<Dictionary<string, object>> itemsParameters = JsonMapper.ToObject<List<Dictionary<string, object>>>(json);
						
						foreach (Dictionary<string, object> parameters in itemsParameters)
						{
							GA_Submit.Item item = new GA_Submit.Item
							{
								Type = category,
								Parameters = parameters,
								AddTime = Time.time
							};
							
							items.Add(item);
						}
					}
				}
			}
			fileReader.Close();
			
			File.Delete(fileName);
		}
		
		return items;
	}
}