/// <summary>
/// This class handles the submit queue. Any data which should be sent to the Game Analytics servers should be added to the queue
/// using the AddItem method. All messages in the queue will be sent at every TIMER interval.
/// </summary>

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public static class GA_Queue
{
	/// <summary>
	/// The time in seconds between each submit to the GA server
	/// </summary>
	public static float TIMER = 5;
	
	/// <summary>
	/// The maximum length of the submit queue. When messages are not submitted due to an error they are put back
	/// into the queue and another attempt at submitting them is made during the next submit interval. When the queue
	/// reaches the maximum length the oldest messages will be dropped.
	/// </summary>
	public static int MAXQUEUESIZE = 100;
	
	/// <summary>
	/// If true the game will automatically quit after data has been submitted to the server
	/// </summary>
	public static bool QUITONSUBMIT = false;
	
	#region private values
	
	/// <summary>
	/// A list containing all the messages which should be submitted to the GA server on the next submit
	/// </summary>
	private static List<GA_Submit.Item> _queue = new List<GA_Submit.Item>();
	
	/// <summary>
	/// A temporary list containing any new messages being recorded during a submit.
	/// These messages will be put back into the queue for the next submit
	/// </summary>
	private static List<GA_Submit.Item> _tempQueue = new List<GA_Submit.Item>();
	
	/// <summary>
	/// A list containing all the failed messages from a submit. These messages will be put
	/// back into the submit queue when another message is submitted succesfully
	/// </summary>
	private static List<GA_Submit.Item> _errorQueue = new List<GA_Submit.Item>();
	
	/// <summary>
	/// While true we are submitting messages to the GA server and new messages should therefore be put into the temporary queue
	/// </summary>
	private static bool _submittingData = false;
	
	/// <summary>
	/// The number of messages we have submitted to the GA server so far during any queue submit 
	/// </summary>
	private static int _submitCount = 0;
	
	/// <summary>
	/// set to true when the game cannot be found, to prevent us from sending messages constantly with no hope of success
	/// </summary>
	private static bool _endsubmit = false;
	
	#endregion
	
	#region public methods
	
	/// <summary>
	/// Add a new message to the submit queue. If we are in the middle of a queue submit we add the message to the temporary queue instead
	/// </summary>
	/// <param name="parameters">
	/// The message is a dictionary of parameters <see cref="Dictionary<System.String, System.Object>"/>
	/// </param>
	/// <param name="type">
	/// The GA service to send the message to (see GA_Submit) <see cref="GA_Submit.CategoryType"/>
	/// </param>
	public static void AddItem(Dictionary<string, object> parameters, GA_Submit.CategoryType type)
	{
		//No reason to add any more items if we have stopped submitting data or we are not supposed to submit in the first place
		if (_endsubmit || (Application.isEditor && !GA.RUNINEDITOR))
		{
			return;
		}
		
		GA_Submit.Item item = new GA_Submit.Item
		{
			Type = type,
			Parameters = parameters,
			AddTime = Time.time
		};
		
		if (_submittingData)
		{
			_tempQueue.Add(item);
		}
		else
		{
			_queue.Add(item);
		}
	}
	
	/// <summary>
	/// At every timer interval we submit the next batch of messages to the GA server
	/// </summary>
	/// <returns>
	/// A <see cref="IEnumerator"/>
	/// </returns>
	public static IEnumerator SubmitQueue()
	{
		//If we're still submitting data then wait half a second and try again
		while (_submittingData)
		{
			yield return new WaitForSeconds(0.5f);
		}
		
		//If we have something to submit and we have not stopped submitting completely then we start submitting data
		if (_queue.Count > 0 && !_submittingData && !_endsubmit)
		{
			_submittingData = true;
		
			if (GA.DEBUG)
			{
				Debug.Log("GA: Queue submit started");
			}
			
			GA_Submit.SubmitQueue(_queue, Submitted, SubmitError);
		}
		
		//Wait for the next timer interval before we try to submit again
		yield return new WaitForSeconds(TIMER);
		
		//If we have not stopped submitting completely then it is time to submit again
		if (!_endsubmit)
		{
			GA.RunCoroutine(SubmitQueue());
		}
	}
	
	#endregion
	
	#region private methods
	
	/// <summary>
	/// This is called once for every successful message submitted. We count the number of messages submitted and when
	/// we are done submitting everything in the queue we put everything in the temporary queue back into the queue
	/// </summary>
	private static void Submitted(List<GA_Submit.Item> items, bool success)
	{
		_submitCount += items.Count;
		
		/* When all items have either been submitted successfully or errors have been stored in the error queue,
		 * we can return to collecting new events normally */
		if (_submitCount >= _queue.Count)
		{
			if (GA.DEBUG)
			{
				Debug.Log("GA: Queue submit over");
			}
			
			//If we were told to quit after this submit then quit
			if (QUITONSUBMIT)
			{
				Application.Quit();
			}
			
			//Reset counters and go back to normal event collection
			_submitCount = 0;
			_submittingData = false;
			
			//Put the items collected while we were submitting in to the queue and clear the temporary queue
			_queue = _tempQueue;
			_tempQueue = new List<GA_Submit.Item>();
			
			//Do not attempt to re-submit errored messages until a message has been submitted successfully
			if (success)
			{
				_queue.AddRange(_errorQueue);
				_errorQueue = new List<GA_Submit.Item>();
			}
		}
	}
	
	/// <summary>
	/// This is called once for every failed message (an error occurs during submit). We put the message into the temporary queue - this way
	/// it will be put back into the queue at the end of the queue submit and on the next queue submit we attempt to submit the message again
	/// </summary>
	/// <param name="item">
	/// The failed message <see cref="GA_Submit.Item"/>
	/// </param>
	private static void SubmitError(List<GA_Submit.Item> items)
	{
		//If items are null we should stop submitting data after this timer interval because the game cannot be found
		if (items == null)
		{
			if (GA.DEBUG)
			{
				Debug.Log("GA: Ending all data submission after this timer interval");
			}
			
			_endsubmit = true;
			return;
		}
		
		_errorQueue.AddRange(items);
		
		/* If the number of error messages exceeds the limit we sort the errors so game design events
		 * are the first to be discarded. Then we remove the access error messages */
		if (_errorQueue.Count > MAXQUEUESIZE)
		{
			_errorQueue.Sort(new ItemComparer());
			_errorQueue.RemoveRange(MAXQUEUESIZE, _errorQueue.Count - MAXQUEUESIZE);
		}
		
		Submitted(items, false);
	}
	
	#endregion
}

class ItemComparer : IComparer<GA_Submit.Item>
{
	public int Compare(GA_Submit.Item item1, GA_Submit.Item item2)
	{
		if (item1.Type != GA_Submit.CategoryType.GA_Event &&
			item2.Type == GA_Submit.CategoryType.GA_Event)
		{
			return 1;
		}
		else if (item2.Type != GA_Submit.CategoryType.GA_Event &&
				 item1.Type == GA_Submit.CategoryType.GA_Event)
		{
			return -1;
		}
		
		float n1 = item1.AddTime;
		float n2 = item2.AddTime;
		
		if (n1 < n2)
		{
			return 1;
		}
		else if (n1 == n2)
		{
			return 0;
		}
		else
		{
			return -1;
		}
	}
}