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

	public class TWStepException : TWException 
	{
	}

	public class TWInvalidStepState : TWStepException 
	{
	}

	
	public class TWGroupException : TWStepException 
	{
	}

	public class TWEmptyGroup : TWGroupException 
	{
	}

	public class TWUnknownFinishCondition : TWGroupException 
	{
	}


	public interface ITWStep
	{
		void Start();
		float Update(float deltaTime);
		bool IsFinished();
	}

	/* A group step, has at least one primary step
	 * All steps execute simultaneously
	 */
	public class TWGroup : ITWStep 
	{
		public enum FinishCondition 
		{
			First, // The first added sub-step finished
			All, // All sub-steps finished
			Any // Any sub-step finished
		}
		List<ITWStep> _steps;
		FinishCondition _finishCondition;
		public TWGroup(FinishCondition finishCondition) 
		{
			_steps = new List<ITWStep>();
			_finishCondition = finishCondition;
		}

		public void Start()
		{
			// Iterate all the substeps and start them
			foreach (ITWStep step in _steps) {
				step.Start();
			}
		}

		public TWGroup AddStep(ITWStep step) {
			_steps.Add(step);
			return this;
		}

		public float Update(float deltaTime) {
			// Update all the substeps
			foreach (ITWStep step in _steps) {
				step.Update(deltaTime);
			}
			return 0;
		}

		public bool IsFinished() {
			// If there are no steps, this is an invalid condition
			if (_steps.Count == 0) 
			{
				throw new TWEmptyGroup();
			}
			// According to the end condition 
			if (_finishCondition == FinishCondition.All) 
			{
				return AllStepsFinished();
			}
			else if (_finishCondition == FinishCondition.Any) 
			{
				return AnyStepFinihsed();
			}
			else if (_finishCondition == FinishCondition.First) 
			{
				return FirstStepFinished();
			}
			else
			{
				throw new TWUnknownFinishCondition();
			}
		}

		private bool FirstStepFinished() {
			// If there are no steps, this is an invalid state
			if (_steps.Count == 0) 
			{
				throw new TWEmptyGroup();
			}
			// Return the IsFinished of the first sub-step
			return _steps[0].IsFinished();
		}

		private bool AnyStepFinihsed() {
			// Check each step, if one is finished return true immediately and dont check the others
			foreach (ITWStep step in _steps) 
			{
				if (step.IsFinished()) 
				{
					return true;
				}
			}
			// If we got here, there is no finished step
			return false;
		}

		private bool AllStepsFinished() {
			// Check each step, if one is unfinished return false immediately and dont check the others
			foreach (ITWStep step in _steps) 
			{
				if (!step.IsFinished()) 
				{
					return false;
				}
			}
			// If we got here, all steps are finished
			return true;
		}
	}

	public class TWWait : ITWStep
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

	public abstract class TWValueProgress<T> : ITWStep
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

	public class TWVector3LineProgress : TWValueProgress<Vector3> 
	{
		public TWVector3LineProgress(Action<Vector3> setterLambda, Func<Vector3> getterLambda, Vector3 to, float duration, Func<float,float> easing) : 
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
		private List<ITWStep> _steps;
		private ITWStep _currentProcess;
		private int _currentProcessIndex = 0;
		private Transform _transform;
		/* Spare deltatime is remaining time of the currently finished step, passed on to the next step for precision of positioning through time
		 * For example, if I asked to move for 2 secs and then scale for 1 sec, and the move had an extra 0.02 seconds - we'll pass it on to the scale so the total 
		 * animation length is exactly 3 seconds (2+1)
		 */

		private float _spareDeltaTime;
		private bool _finished = false;

		public TWSequence(Transform transform) 
		{
			_steps = new List<ITWStep>();
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
			ITWStep step = new TWVector3LineProgress(setterLambda, getterLambda, to, duration, easing);
			return AddStep(step);
		}

		public TWSequence FloatTo(Action<float> setterLambda, Func<float> getterLambda, float to, float duration, Func<float,float> easing) 
		{
			ITWStep step = new TWFloatProgress(setterLambda, getterLambda, to, duration, easing);
			return AddStep(step);
		}

		public TWSequence Wait(float duration) 
		{
			ITWStep step = new TWWait(duration);
			return AddStep(step);
		}

		public TWSequence AddStep(ITWStep step)
		{
			_steps.Add(step);
			return this;
		}

		public void Update(float deltaTime) 
		{
			// Raise an exception if finished, you cannot play a finished seq.
			if (IsFinihsed()) {
				throw new TWEmptySequenceException(); 
			}
			// If the current process is a new one, call Start on it.
			if (_currentProcess != _steps[_currentProcessIndex]) 
			{
				_currentProcess = _steps[_currentProcessIndex];
				_currentProcess.Start ();
			}

			// Move to the next step if the step is finished
			if (_currentProcess.IsFinished()) 
			{
				_currentProcessIndex++;
				if (_currentProcessIndex >= _steps.Count) {
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
