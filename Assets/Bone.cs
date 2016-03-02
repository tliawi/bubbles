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
			boneLength = source.distance (target);
		}

		public Node otherEnd(Node oneEnd){
			if (oneEnd == source ) return target;
			if (oneEnd == target) return source;
			return null;
		}

		public bool isExternal(){
			return target.org != source.org;
		}

		public bool isInternal(){
			return target.org == source.org;
		}

			
		// Elastically pushes or pulls its source/target pair, to maintain the distance between them at boneLength.
		public void action()
		{	
			float dx = target.x - source.x;
			float dy = target.y - source.y;
			float dislocation, effect, dist, sourceEffect, targetEffect;

			if (dx == 0 && dy == 0) { //bone has no notion of what direction to push
				float v = Random.Range (-Mathf.PI, Mathf.PI);
				dx = Mathf.Cos(v)*Node.minPosValue; dy = Mathf.Sin(v)*Node.minPosValue; 
				dist = Node.minPosValue;
			}
			else dist = source.distance(target);

			dislocation = boneLength - dist;

			effect = boneStiffness*dislocation;// - if too long, + if too short.
			//independent of efficiency, and is proportional to dislocation. Dislocation has an upper bound if bone is compressed, but 
			//no upper bound if bone is stretched

			// Effect on one end is independent of effect on the other.
			// Each experiences the same 'force', they react to it in inverse proportion to their burden.
			//A smaller burden moves more than a bigger burden. Bone force is not per unit of burden,
			//it is structural, i.e. bones between large masses are, in muscle terms, much stronger than
			//bones between small burdens.

			sourceEffect = effect * target.burden / (source.burden + target.burden);
			targetEffect = effect * source.burden / (source.burden + target.burden);

			source.nx -= sourceEffect * dx/dist;
			source.ny -= sourceEffect * dy/dist;

		
			target.nx += targetEffect * dx/dist;
			target.ny += targetEffect * dy/dist;

		}
	}
}