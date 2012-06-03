/// <summary>
/// This class handles game design events, such as kills, deaths, high scores, etc.
/// </summary>

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class GA_Design
{
	#region public methods
	
	public static void NewEvent(string eventName, float? eventValue, float x, float y, float z)
	{
		CreateNewEvent(eventName, eventValue, x, y, z);
	}
	
	public static void NewEvent(string eventName, float eventValue)
	{
		CreateNewEvent(eventName, eventValue, null, null, null);
	}
	
	public static void NewEvent(string eventName)
	{
		CreateNewEvent(eventName, null, null, null, null);
	}
	
	#endregion
	
	#region private methods
	
	/// <summary>
	/// Adds a custom event to the submit queue (see GA_Queue)
	/// </summary>
	/// <param name="eventName">
	/// Identifies the event so this should be as descriptive as possible. PickedUpAmmo might be a good event name. EventTwo is a bad event name! <see cref="System.String"/>
	/// </param>
	/// <param name="eventValue">
	/// A value relevant to the event. F.x. if the player picks up some shotgun ammo the eventName could be "PickedUpAmmo" and this value could be "Shotgun". This can be null <see cref="System.Nullable<System.Single>"/>
	/// </param>
	/// <param name="x">
	/// The x coordinate of the event occurence. This can be null <see cref="System.Nullable<System.Single>"/>
	/// </param>
	/// <param name="y">
	/// The y coordinate of the event occurence. This can be null <see cref="System.Nullable<System.Single>"/>
	/// </param>
	private static void CreateNewEvent(string eventName, float? eventValue, float? x, float? y, float? z)
	{
		Dictionary<string, object> parameters = new Dictionary<string, object>()
		{
			{ GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.UserID], GA_GenericInfo.UserID },
			{ GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.SessionID], GA_GenericInfo.SessionID },
			{ GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Build], GA.BUILD },
			{ GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.EventID], eventName },
			{ GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Level], Application.loadedLevelName }
		};
		
		if (eventValue.HasValue)
		{
			parameters.Add(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Value], eventValue.ToString());
		}
		
		if (x.HasValue)
		{
			parameters.Add(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.X], x.ToString());
		}
		
		if (y.HasValue)
		{
			parameters.Add(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Y], y.ToString());
		}
		
		if (z.HasValue)
		{
			parameters.Add(GA_ServerFieldTypes.Fields[GA_ServerFieldTypes.FieldType.Z], z.ToString());
		}
		
		GA_Queue.AddItem(parameters, GA_Submit.CategoryType.GA_Event);
	}
	
	#endregion
}