//jf modified version 002

/*
 * The author of this software is Steven Fortune.  Copyright (c) 1994 by AT&T
 * Bell Laboratories.
 * Permission to use, copy, modify, and distribute this software for any
 * purpose without fee is hereby granted, provided that this entire notice
 * is included in all copies of any software which is or includes a copy
 * or modification of this software and in all copies of the supporting
 * documentation for such software.
 * THIS SOFTWARE IS BEING PROVIDED "AS IS", WITHOUT ANY EXPRESS OR IMPLIED
 * WARRANTY.  IN PARTICULAR, NEITHER THE AUTHORS NOR AT&T MAKE ANY
 * REPRESENTATION OR WARRANTY OF ANY KIND CONCERNING THE MERCHANTABILITY
 * OF THIS SOFTWARE OR ITS FITNESS FOR ANY PARTICULAR PURPOSE.
 */

using UnityEngine;
using System;
using System.Collections.Generic;
using Delaunay.Geo;
using Delaunay.Utils;
using Delaunay.LR;

namespace Delaunay
{
	public sealed class Voronoi: Utils.IDisposable
	{
		private SiteList _sites;
		private Dictionary <Vector2,Site> _sitesIndexedByLocation;
		private List<Triangle> _triangles;
		private List<Edge> _edges;

		
		// TODO generalize this so it doesn't have to be a rectangle;
		// then we can make the fractal voronois-within-voronois
		private Rect _plotBounds;
		public Rect plotBounds {
			get { return _plotBounds;}
		}

		//jfcomment: This is never called, so pools are not maintained. I tried calling it before every use of new Voronoi, but it only slowed things down.
		//Some of the _pools are used during voronoi computation, but the _sites._sites pool is only pushed into on _sites.Dispose. 
		//_pools are statics. Since I now maintain pointers to sites in Engine.nodes, there is no need to pool sites, and calling _sites.Dispose
		//would fill up the _sites pool, and make a memory leak. 
		//I think the coder of Voronoi made a mistake to abuse iDispose and dispose to frustrate the gc (keep pools), it's the exact opposite of their normal usage. 
		//It would have been better to name them "recycle" or something.
		public void Dispose ()
		{   bubbleServer.debugDisplay("ERROR, Voronoi.Dispose!!");
			int i, n;
			if (_sites != null) {
				_sites.Dispose ();
				_sites = null;
			}
			if (_triangles != null) {
				n = _triangles.Count;
				for (i = 0; i < n; ++i) {
					_triangles [i].Dispose ();
				}
				_triangles.Clear ();
				_triangles = null;
			}
			if (_edges != null) {
				n = _edges.Count;
				for (i = 0; i < n; ++i) {
					_edges [i].Dispose ();
				}
				_edges.Clear ();
				_edges = null;
			}
//			_plotBounds = null;
			_sitesIndexedByLocation = null;
		}
		
		public Voronoi (List<Bub.Node> nodes, Rect plotBounds) //jf
		{
			if (_sites == null) _sites = new SiteList (); //thereafter maintain it in parallel with nodes
			_sitesIndexedByLocation = new Dictionary <Vector2,Site> (); // XXX: Used to be Dictionary(true) -- weak refs. 
			AddSites (nodes);//jf
			_plotBounds = plotBounds;
			_triangles = new List<Triangle> ();
			_edges = new List<Edge> ();
			FortunesAlgorithm ();
		}
		
		private void AddSites (List<Bub.Node> nodes) //jf
		{	Vector2 p = new Vector2();
			
			for (int i = 0; i < nodes.Count; ++i) {
				Bub.Node node = nodes[i];
				p.x = node.x; p.y = node.y;
				//move toward zero, so stay within plotBounds. Guarantee unique position of all nodes
				while (_sitesIndexedByLocation.ContainsKey(p)) p.x += (p.x<0)? UnityEngine.Random.Range(0,0.000001f): -UnityEngine.Random.Range(0,0.000001f);
				if (node.site == null) node.site = Site.Create (p, (uint) i, node );
				else node.site.Init(p, (uint) i, node );// both node and site reference each other
				if (i >= _sites.Count) _sites.Add (node.site);
				else _sites.Plant(node.site,i);
				_sitesIndexedByLocation [p] = node.site;
			}

		}

		public List<Edge> Edges ()
		{
			return _edges;
		}
          
//		public List<Vector2> Region (Vector2 p)
//		{
//			Site site = _sitesIndexedByLocation [p];
//			if (site == null) {
//				return new List<Vector2> ();
//			}
//			return site.Region (_plotBounds);
//		}
//
//		// TODO: bug: if you call this before you call region(), something goes wrong :(
//		public List<Vector2> NeighborSitesForSite (Vector2 coord)
//		{
//			List<Vector2> points = new List<Vector2> ();
//			Site site = _sitesIndexedByLocation [coord];
//			if (site == null) {
//				return points;
//			}
//			List<Site> sites = site.NeighborSites ();
//			Site neighbor;
//			for (int nIndex =0; nIndex<sites.Count; nIndex++) {
//				neighbor = sites [nIndex];
//				points.Add (neighbor.Coord);
//			}
//			return points;
//		} jf commented out

		public List<Circle> Circles ()
		{
			return _sites.Circles ();
		}
		
		public List<LineSegment> VoronoiBoundaryForSite (Vector2 coord)
		{
			return DelaunayHelpers.VisibleLineSegments (DelaunayHelpers.SelectEdgesForSitePoint (coord, _edges));
		}

		public List<LineSegment> DelaunayLinesForSite (Vector2 coord)
		{
			return DelaunayHelpers.DelaunayLinesForEdges (DelaunayHelpers.SelectEdgesForSitePoint (coord, _edges));
		}
		
		public List<LineSegment> VoronoiDiagram ()
		{
			return DelaunayHelpers.VisibleLineSegments (_edges);
		}
		
		public List<LineSegment> DelaunayTriangulation (/*BitmapData keepOutMask = null*/)
		{
			return DelaunayHelpers.DelaunayLinesForEdges (DelaunayHelpers.SelectNonIntersectingEdges (/*keepOutMask,*/_edges));
		}
		
		public List<LineSegment> Hull ()
		{
			return DelaunayHelpers.DelaunayLinesForEdges (HullEdges ());
		}
		
		private List<Edge> HullEdges ()
		{
			return _edges.FindAll (delegate (Edge edge) {
				return (edge.IsPartOfConvexHull ());
			});
		}

		public List<Vector2> HullPointsInOrder ()
		{
			List<Edge> hullEdges = HullEdges ();
			
			List<Vector2> points = new List<Vector2> ();
			if (hullEdges.Count == 0) {
				return points;
			}
			
			EdgeReorderer reorderer = new EdgeReorderer (hullEdges, VertexOrSite.SITE);
			hullEdges = reorderer.edges;
			List<Side> orientations = reorderer.edgeOrientations;
			reorderer.Dispose ();
			
			Side orientation;

			int n = hullEdges.Count;
			for (int i = 0; i < n; ++i) {
				Edge edge = hullEdges [i];
				orientation = orientations [i];
				points.Add (edge.Site (orientation).Coord);
			}
			return points;
		}
		
		public List<LineSegment> SpanningTree (KruskalType type = KruskalType.MINIMUM/*, BitmapData keepOutMask = null*/)
		{
			List<Edge> edges = DelaunayHelpers.SelectNonIntersectingEdges (/*keepOutMask,*/_edges);
			List<LineSegment> segments = DelaunayHelpers.DelaunayLinesForEdges (edges);
			return DelaunayHelpers.Kruskal (segments, type);
		}

		public List<List<Vector2>> Regions ()
		{
			return _sites.Regions (_plotBounds);
		}
		
//		public List<uint> SiteColors (/*BitmapData referenceImage = null*/)
//		{
//			return _sites.SiteColors (/*referenceImage*/);
//		} jf
		
		/**
		 * 
		 * @param proximityMap a BitmapData whose regions are filled with the site index values; see PlanePointsCanvas::fillRegions()
		 * @param x
		 * @param y
		 * @return coordinates of nearest Site to (x, y)
		 * 
		 */
		public Nullable<Vector2> NearestSitePoint (/*BitmapData proximityMap,*/float x, float y)
		{
			return _sites.NearestSitePoint (/*proximityMap,*/x, y);
		}
		
		public List<Vector2> SiteCoords ()
		{
			return _sites.SiteCoords ();
		}

		private Site fortunesAlgorithm_bottomMostSite;
		private void FortunesAlgorithm ()
		{
			Site newSite, bottomSite, topSite, tempSite;
			Vertex v, vertex;
			Vector2 newintstar = Vector2.zero; //Because the compiler doesn't know that it will have a value - Julian
			Side leftRight;
			Halfedge lbnd, rbnd, llbnd, rrbnd, bisector;
			Edge edge;
			
			Rect dataBounds = _sites.GetSitesBounds ();
			
			int sqrt_nsites = (int)(Mathf.Sqrt (_sites.Count + 4));
			HalfedgePriorityQueue heap = new HalfedgePriorityQueue (dataBounds.y, dataBounds.height, sqrt_nsites);
			EdgeList edgeList = new EdgeList (dataBounds.x, dataBounds.width, sqrt_nsites);
			List<Halfedge> halfEdges = new List<Halfedge> ();
			List<Vertex> vertices = new List<Vertex> ();
			
			fortunesAlgorithm_bottomMostSite = _sites.Next ();
			newSite = _sites.Next ();
			
			for (;;) {
				if (heap.Empty () == false) {
					newintstar = heap.Min ();
				}
			
				if (newSite != null 
					&& (heap.Empty () || CompareByYThenX (newSite, newintstar) < 0)) {
					/* new site is smallest */
					//trace("smallest: new site " + newSite);
					
					// Step 8:
					lbnd = edgeList.EdgeListLeftNeighbor (newSite.Coord);	// the Halfedge just to the left of newSite
					//trace("lbnd: " + lbnd);
					rbnd = lbnd.edgeListRightNeighbor;		// the Halfedge just to the right
					//trace("rbnd: " + rbnd);
					bottomSite = FortunesAlgorithm_rightRegion (lbnd);		// this is the same as leftRegion(rbnd)
					// this Site determines the region containing the new site
					//trace("new Site is in region of existing site: " + bottomSite);
					
					// Step 9:
					edge = Edge.CreateBisectingEdge (bottomSite, newSite);
					//trace("new edge: " + edge);
					_edges.Add (edge);
					
					bisector = Halfedge.Create (edge, Side.LEFT);
					halfEdges.Add (bisector);
					// inserting two Halfedges into edgeList constitutes Step 10:
					// insert bisector to the right of lbnd:
					edgeList.Insert (lbnd, bisector);
					
					// first half of Step 11:
					if ((vertex = Vertex.Intersect (lbnd, bisector)) != null) {
						vertices.Add (vertex);
						heap.Remove (lbnd);
						lbnd.vertex = vertex;
						lbnd.ystar = vertex.y + newSite.Dist (vertex);
						heap.Insert (lbnd);
					}
					
					lbnd = bisector;
					bisector = Halfedge.Create (edge, Side.RIGHT);
					halfEdges.Add (bisector);
					// second Halfedge for Step 10:
					// insert bisector to the right of lbnd:
					edgeList.Insert (lbnd, bisector);
					
					// second half of Step 11:
					if ((vertex = Vertex.Intersect (bisector, rbnd)) != null) {
						vertices.Add (vertex);
						bisector.vertex = vertex;
						bisector.ystar = vertex.y + newSite.Dist (vertex);
						heap.Insert (bisector);	
					}
					
					newSite = _sites.Next ();	
				} else if (heap.Empty () == false) {
					/* intersection is smallest */
					lbnd = heap.ExtractMin ();
					llbnd = lbnd.edgeListLeftNeighbor;
					rbnd = lbnd.edgeListRightNeighbor;
					rrbnd = rbnd.edgeListRightNeighbor;
					bottomSite = FortunesAlgorithm_leftRegion (lbnd);
					topSite = FortunesAlgorithm_rightRegion (rbnd);
					// these three sites define a Delaunay triangle
					// (not actually using these for anything...)
					//_triangles.push(new Triangle(bottomSite, topSite, rightRegion(lbnd)));
					
					v = lbnd.vertex;
					v.SetIndex ();
					lbnd.edge.SetVertex ((Side)lbnd.leftRight, v);
					rbnd.edge.SetVertex ((Side)rbnd.leftRight, v);
					edgeList.Remove (lbnd); 
					heap.Remove (rbnd);
					edgeList.Remove (rbnd); 
					leftRight = Side.LEFT;
					if (bottomSite.y > topSite.y) {
						tempSite = bottomSite;
						bottomSite = topSite;
						topSite = tempSite;
						leftRight = Side.RIGHT;
					}
					edge = Edge.CreateBisectingEdge (bottomSite, topSite);
					_edges.Add (edge);
					bisector = Halfedge.Create (edge, leftRight);
					halfEdges.Add (bisector);
					edgeList.Insert (llbnd, bisector);
					edge.SetVertex (SideHelper.Other (leftRight), v);
					if ((vertex = Vertex.Intersect (llbnd, bisector)) != null) {
						vertices.Add (vertex);
						heap.Remove (llbnd);
						llbnd.vertex = vertex;
						llbnd.ystar = vertex.y + bottomSite.Dist (vertex);
						heap.Insert (llbnd);
					}
					if ((vertex = Vertex.Intersect (bisector, rrbnd)) != null) {
						vertices.Add (vertex);
						bisector.vertex = vertex;
						bisector.ystar = vertex.y + bottomSite.Dist (vertex);
						heap.Insert (bisector);
					}
				} else {
					break;
				}
			}
			
			// heap should be empty now
			heap.Dispose ();
			edgeList.Dispose ();
			
			for (int hIndex = 0; hIndex<halfEdges.Count; hIndex++) {
				Halfedge halfEdge = halfEdges [hIndex];
				halfEdge.ReallyDispose ();
			}
			halfEdges.Clear ();
			
			// we need the vertices to clip the edges
			for (int eIndex = 0; eIndex<_edges.Count; eIndex++) {
				edge = _edges [eIndex];
				edge.ClipVertices (_plotBounds);
			}
			// but we don't actually ever use them again!
			for (int vIndex = 0; vIndex<vertices.Count; vIndex++) {
				vertex = vertices [vIndex];
				vertex.Dispose ();
			}
			vertices.Clear ();
		}

		private Site FortunesAlgorithm_leftRegion (Halfedge he)
		{
			Edge edge = he.edge;
			if (edge == null) {
				return fortunesAlgorithm_bottomMostSite;
			}
			return edge.Site ((Side)he.leftRight);
		}
		
		private Site FortunesAlgorithm_rightRegion (Halfedge he)
		{
			Edge edge = he.edge;
			if (edge == null) {
				return fortunesAlgorithm_bottomMostSite;
			}
			return edge.Site (SideHelper.Other ((Side)he.leftRight));
		}

		public static int CompareByYThenX (Site s1, Site s2)
		{
			if (s1.y < s2.y)
				return -1;
			if (s1.y > s2.y)
				return 1;
			if (s1.x < s2.x)
				return -1;
			if (s1.x > s2.x)
				return 1;
			return 0;
		}

		public static int CompareByYThenX (Site s1, Vector2 s2)
		{
			if (s1.y < s2.y)
				return -1;
			if (s1.y > s2.y)
				return 1;
			if (s1.x < s2.x)
				return -1;
			if (s1.x > s2.x)
				return 1;
			return 0;
		}

//		//jf Oct 2015
//		// I abuse color to hold node ids (indices into nodes).
//		// Given the x,y location of a node, returns the ids of neighboring nodes.
//		public List<int> NeighborIDs (Vector2 coord)
//		{
//			List<int> ids = new List<int> ();
//			Site site = _sitesIndexedByLocation [coord];
//			if (site == null) {
//				return ids;
//			}
//
//			ids.Add ((int)site.color); //list starts with the id of the source.
//			// It is conceivable that two sources have exactly the same coordinates, this permits discovery thereof
//
//			List<Site> nsites = site.NeighborSites ();
//			for (int i =0; i<nsites.Count; i++) {
//				ids.Add ((int)nsites [i].color);
//			}
//			return ids;
//		} jf jan 2016 color replaced by node reference

	}
}