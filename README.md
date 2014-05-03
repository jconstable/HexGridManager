HexGridManager
==============

Create a logical hex grid on top of Unity's NavMesh.

The problem:

Unity's NavMesh is great for quickly adding locomotion to games objects, and their NavMesh creation tool is 
extremely easy to use. However, in practice, if you have a lot of NavAgents in the scene, you have three options:
1) Increase the NavAgent radius, which will slow the game down and create weird behavior if destinations
    are too close to one another
2) Reduce the NavAgent radius and have objects end up stacking on top of each other
3) Use another pathfinding solution, which is probably written in C# and not as fast as Unity's C++ solution

A solution:

I created this HexGridManager as a lightweight way to track moving GameObjects and ensure that GameObjects try not to 
stack up, without affecting pathfinding. The main use case is to find locations on the nav mesh that are near, or next
to, a target, and have some level of confidence that the area will not be too close to other such objects. 

The size of the hex unit is configurable, and the hex map can (and should) be pre-baked into the scene, avoding the 
cost at runtime.

How to use this:

The basic concept is that for any given GameObject, you can create an Occupant within the manager. The occupant uses 
the GameObject's position to map it to a logical hex grid in the map. The Occupant can be created with a magnitude, in
order to reserve a greater area around itself on the map.

If another GameObject would like to path to a location next to another object, the HexGridManager can be queried for a
vacant neighboring hex, which would be used as the destination.

Whenever an Occupant detects a new grid hexes, the GameObject is sent the "OnGridChanged" message, allowing it to
take action.

This repo contains a sample project with two scenes: 
1) RandomMovement - a basic demo of many navigating GameObjects and what the grid map looks like as they path around
2) Swarming - a target GameObject is swarmed by many other objects.

A ready-to-use UnityPackage has also been provided.

Special thanks to this blog post by Red Blob Games for execellent hex math:
http://www.redblobgames.com/grids/hexagons/

And to Baran Kahyaoglu for sample code to generate hex meshes on the fly (for debug visuals)
http://www.barankahyaoglu.com/blog/post/2013/07/26/Hexagons-in-Unity.aspx

TODO:
- Functions to return grid distance between two points
- Function to return best possible grid paths between two points

