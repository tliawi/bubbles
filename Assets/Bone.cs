//copyright 2016 John Fairfield

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{
public class Bone{

	public static float boneStiffness = 0.35f;

	public Node source {get; private set;}
	public Node target {get; private set;}
	public float boneLength { get; private set; }

	public Bone (Node source0, Node target0){
		source = source0;
		target = target0;
		boneLength = source.distance(target);
	}

	//only for bone links. Elastically pushes or pulls its source/target pair, to maintain the distance between them at boneLength
	public void action()
	{	float dx = target.x - source.x;
		float dy = target.y - source.y;
		float dislocation, effect, dist;

		if (dx == 0 && dy == 0) { //bone has no notion of what direction to push
			float v = Random.Range (-Mathf.PI, Mathf.PI);
			dx = Mathf.Cos(v)*Node.minPosValue; dy = Mathf.Sin(v)*Node.minPosValue; 
			dist = Node.minPosValue;
		}
		else dist = source.distance(target);

		dislocation = boneLength - dist;

		effect = boneStiffness*dislocation;// - if too long, + if too short.

		// Effect on one end is independent of effect on the other.
		// Each experiences the same 'force', they react to it in inverse proportion to their burden.
		//A smaller burden moves more than a bigger burden. Bone force is not per unit of burden,
		//it is structural, i.e. bones between large masses are, in muscle terms, much stronger than
		//bones between small burdens.

		effect *= target.burden /(source.burden + target.burden);

		source.nx -= effect*dx/dist;
		source.ny -= effect*dy/dist;

	
		//target will be moved when target processes this bone's twin
		//			target.nx += dx*effect/target.burden;
		//			target.ny += dy*effect/target.burden;

	}

	}}