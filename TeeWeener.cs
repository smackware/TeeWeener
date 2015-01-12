using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


/* Usage example
 * TeeWeener.with(_myTransform).MoveTo(Vector3.zero, 3, CurvePresets.Linear).Wait(4).Start()
 */

public class TeeWeener 
{
	public static readonly TeeWeenerController controller;
	
	static TeeWeener() {
		GameObject go = new GameObject("TeeWeenerController");
		controller = go.AddComponent<TeeWeenerController>();
	}

	public static TWSequence with(Transform transform) 
	{
		return new TWSequence(transform);
	}

	public class TWException : Exception 
	{
	}

	public class TWEmptySequenceException : TWException 
	{
	}

	interface ITWProgress
	{
		void Start();
		float Update(float deltaTime);
		bool IsFinished();
	}


	public class TWWait : ITWProgress
	{
		/*
		 * A simple sleep step
		 */
		private readonly float _duration;
		private float _runningTime;
		
		public TWWait(float duration) 
		{
			_duration = duration;
		}

		public void Start() 
		{
			_runningTime = 0;
		}


		public float Update(float deltaTime) 
		{
			float remainingTime = _duration - _runningTime;
			float usedDeltaTime = Mathf.Min(deltaTime, remainingTime);
			float excessDeltaTime = deltaTime - usedDeltaTime;
			_runningTime += usedDeltaTime;
			return excessDeltaTime;
		}
		
		public bool IsFinished() 
		{
			return _runningTime >= _duration;
		}
	}

	public abstract class TWValueProgress<T> : ITWProgress
	{

		private readonly T _to;
		private readonly float _duration;
		private readonly Func<float,float> _easing;
		private readonly Action<T> _setterLambda;
		private readonly Func<T> _getterLambda;
		private float _runningTime;
		private T _from;


		public TWValueProgress(Action<T> setterLambda, Func<T> getterLambda, T to, float duration, Func<float,float> easing) 
		{
			_setterLambda = setterLambda;
			_getterLambda = getterLambda;
			_to = to;
			_duration = duration;
			_easing = easing;
		}

		public void Start() {
			_runningTime = 0;
			_from = _getterLambda();
		}

		public float Update(float deltaTime) 
		{
			float remainingTime = _duration - _runningTime;
			float usedDeltaTime = Mathf.Min(deltaTime, remainingTime);
			float excessDeltaTime = deltaTime - usedDeltaTime;
			_runningTime += usedDeltaTime;
			float transitionPct = _runningTime / _duration;

			float distancePct = _easing(transitionPct);

			T result = SetState(_from, _to, distancePct);
			_setterLambda(result);
			return excessDeltaTime;

		}
		public abstract T SetState(T from, T to, float distancePct);

		public bool IsFinished() 
		{
			return _runningTime >= _duration;
		}
	}

	public class TWVector3Progress : TWValueProgress<Vector3> 
	{
		public TWVector3Progress(Action<Vector3> setterLambda, Func<Vector3> getterLambda, Vector3 to, float duration, Func<float,float> easing) : 
			base(setterLambda, getterLambda, to, duration, easing) 
		{
		}

		public override Vector3 SetState(Vector3 from, Vector3 to, float distancePct) 
		{
			Vector3 delta = to - from;
			return from + delta * distancePct;
		}
	}


	public class TWFloatProgress : TWValueProgress<float> 
	{
		public TWFloatProgress(Action<float> setterLambda, Func<float> getterLambda, float to, float duration, Func<float,float> easing) : 
			base(setterLambda, getterLambda, to, duration, easing) 
		{
		}
		
		public override float SetState(float from, float to, float distancePct) 
		{
			float delta = to - from;
			return from + delta * distancePct;
		}
	}

	public class TWSequence
	{
		private List<ITWProgress> _processes;
		private ITWProgress _currentProcess;
		private int _currentProcessIndex = 0;
		private Transform _transform;
		// Spare deltatime is remaining time of the currently finished step, passed on to the next step for precision of positioning through time
		private float _spareDeltaTime;
		private bool _finished = false;

		public TWSequence(Transform transform) 
		{
			_processes = new List<ITWProgress>();
			_transform = transform;
			_spareDeltaTime = 0;
		}

		public void Start() 
		{
			_currentProcessIndex = 0;
			_finished = false;
			controller.Add(this);
		}

		public TWSequence MoveTo(Vector3 to, float duration, Func<float,float> easing) 
		{
			Action<Vector3> setterLambda = x => _transform.position = x;
			Func<Vector3> getterLambda = () => _transform.position;
			return Vector3To(setterLambda, getterLambda, to, duration, easing);
		}
		
		public TWSequence ScaleTo(Vector3 to, float duration, Func<float,float> easing) 
		{
			Action<Vector3> setterLambda = x => _transform.localScale = x;
			Func<Vector3> getterLambda = () => _transform.localScale;
			return Vector3To(setterLambda, getterLambda, to, duration, easing);
		}


		public TWSequence Vector3To(Action<Vector3> setterLambda, Func<Vector3> getterLambda, Vector3 to, float duration, Func<float,float> easing) 
		{
			ITWProgress step = new TWVector3Progress(setterLambda, getterLambda, to, duration, easing);
			_processes.Add(step);
			return this;
		}

		public TWSequence FloatTo(Action<float> setterLambda, Func<float> getterLambda, float to, float duration, Func<float,float> easing) 
		{
			ITWProgress step = new TWFloatProgress(setterLambda, getterLambda, to, duration, easing);
			_processes.Add(step);
			return this;
		}

		public TWSequence Wait(float duration) 
		{
			ITWProgress step = new TWWait(duration);
			_processes.Add(step);
			return this;
		}

		public void Update(float deltaTime) 
		{
			// Raise an exception if finished, you cannot play a finished seq.
			if (IsFinihsed()) {
				throw new TWEmptySequenceException(); 
			}
			// If the current process is a new one, call Start on it.
			if (_currentProcess != _processes[_currentProcessIndex]) 
			{
				_currentProcess = _processes[_currentProcessIndex];
				_currentProcess.Start ();
			}

			// Move to the next step if the step is finished
			if (_currentProcess.IsFinished()) 
			{
				_currentProcessIndex++;
				if (_currentProcessIndex >= _processes.Count) {
					_finished = true;
					return;
				}
			} 
			else // Otherwise - run update of the process
			{
				_spareDeltaTime = _currentProcess.Update(_spareDeltaTime + deltaTime);
			}
		}

		public bool IsFinihsed() 
		{
			return _finished;
		}
	}
}
