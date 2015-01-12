using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeeWeenerController : MonoBehaviour 
{

	private List<TeeWeener.TWSequence> _sequences = new List<TeeWeener.TWSequence>();


	public void Add(TeeWeener.TWSequence seq) {
		_sequences.Add(seq);
	}

	void Update() 
	{

		int i = 0;
		while (_sequences.Count > i) 
		{
			TeeWeener.TWSequence currentSequence = _sequences[i];
			if (currentSequence.IsFinihsed()) {
				_sequences.RemoveAt(i);
			} else {
				currentSequence.Update(Time.deltaTime);
				i++;
			}
		}
	}
	


}