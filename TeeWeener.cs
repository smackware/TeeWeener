using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class TeeWeener 
{
	public static readonly TeeWeenerController controller;

	static TeeWeener() {
		Debug.Log("ASDASD");
		GameObject go = new GameObject("TeeWeenerController");
		controller = go.AddComponent<TeeWeenerController>();
	}

	public static TWSequence use(Transform transform) {
		return new TWSequence(transform);
	}

	public class TWException : Exception {
	}

	public class TWEmptySequenceException : TWException {
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
			_runningTime = 0;
		}

		public void Start() {
		}

		public float Update(float deltaTime) 
		{
			float remainingTime = _duration - _runningTime;
			float usedDeltaTime = Mathf.Min(deltaTime, remainingTime);
			float excessDeltaTime = deltaTime - usedDeltaTime;
			_runningTime += usedDeltaTime;
			return excessDeltaTime;
		}
		
		public bool IsFinished() {
			return _runningTime >= _duration;
		}
	}

	public interface ICurve {
		float GetFor(float x);
	}

	public class LinearCurve : ICurve {
		public float GetFor(float x) {
			return x;
		}
	}

	public class SinCurve : ICurve {
		public float GetFor(float x) {		
			return Mathf.Sin ( x*90 * Mathf.Deg2Rad );
		}
	}

	public static class CurvePresets {
		public static ICurve Linear = new LinearCurve();
	}

	public abstract class TWValueProgress<T> : ITWProgress
	{

		private readonly T _to;
		private readonly float _duration;
		private readonly ICurve _curve;
		private readonly Action<T> _setterLambda;
		private readonly Func<T> _getterLambda;
		private float _runningTime;
		private T _from;


		public TWValueProgress(Action<T> setterLambda, Func<T> getterLambda, T to, float duration, ICurve curve) 
		{
			_setterLambda = setterLambda;
			_getterLambda = getterLambda;
			_to = to;
			_duration = duration;
			_curve = curve;
			_runningTime = 0;
		}

		public void Start() {
			_from = _getterLambda();
		}

		public float Update(float deltaTime) 
		{
			float remainingTime = _duration - _runningTime;
			float usedDeltaTime = Mathf.Min(deltaTime, remainingTime);
			float excessDeltaTime = deltaTime - usedDeltaTime;
			_runningTime += usedDeltaTime;
			float transitionPct = _runningTime / _duration;

			float distancePct = _curve.GetFor(transitionPct);

			T result = SetState(_from, _to, distancePct);
			_setterLambda(result);
			Debug.Log(transitionPct + " " + distancePct + " " + remainingTime + " " + excessDeltaTime);
			return excessDeltaTime;

		}
		public abstract T SetState(T from, T to, float distancePct);

		public bool IsFinished() {
			return _runningTime >= _duration;
		}
	}

	public class TWVector3Progress : TWValueProgress<Vector3> 
	{
		public TWVector3Progress(Action<Vector3> setterLambda, Func<Vector3> getterLambda, Vector3 to, float duration, ICurve curve) : 
			base(setterLambda, getterLambda, to, duration, curve) 
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
		public TWFloatProgress(Action<float> setterLambda, Func<float> getterLambda, float to, float duration, ICurve curve) : 
			base(setterLambda, getterLambda, to, duration, curve) 
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
		private Transform _transform;
		private float _spareDeltaTime; // Spare deltatime is remaining time of the currently finished step, passed on to the next step

		public TWSequence(Transform transform) {
			_processes = new List<ITWProgress>();
			_transform = transform;
			_spareDeltaTime = 0;
		}

		public void Start() {
			controller.Add(this);
			Debug.Log ("here");
		}

		ICurve GetCurveOrDefaultToLinear(ICurve curve) 
		{
			if (curve == null) 
			{
				return CurvePresets.Linear;
			}
			return curve;
		}

		public TWSequence MoveTo(Vector3 pos, float duration, ICurve curve) {
			curve = GetCurveOrDefaultToLinear(curve);
			Action<Vector3> setterLambda = x => _transform.position = x;
			Func<Vector3> getterLambda = () => _transform.position;
			ITWProgress step = new TWVector3Progress(setterLambda, getterLambda, pos, duration, curve);
			_processes.Add(step);
			return this;
		}

		public void Update(float deltaTime) {
			// Raise an exception if finished, you cannot play a finished seq.
			if (IsFinihsed()) {
				throw new TWEmptySequenceException(); 
			}
			// Play the current step
			ITWProgress currentStep = _processes[0];
			_spareDeltaTime = currentStep.Update(_spareDeltaTime + deltaTime);
			// Cleanup the step if its finished
			if (currentStep.IsFinished()) {
				_processes.RemoveAt(0);
			}
		}

		public bool IsFinihsed() {
			return _processes.Count == 0;
		}
	}
}
