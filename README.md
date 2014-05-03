HexGridManager
==============

Create a logical hex grid on top of Unity's NavMesh.


The problem:

Unity's NavMesh is great for quickly adding locomotion to GameObjects, and Unity's NavMesh creation tool is 
extremely easy to use. However, in practice, if you have a lot of NavAgents in a scene and start seeing odd
behavior, you have three options:

1) Increase the NavAgent radius, which will slow the game down and create strange behavior if destinations
    are too close to one another
2) Reduce the NavAgent radius and have objects end up stacking on top of each other
3) Use another pathfinding solution, which is probably written in C# and not as fast as Unity's C++ solution


A solution:

I created this HexGridManager as a lightweight way give GameObjects destinations that result in less stacking 
up, without affecting pathfinding. The main use case is to find locations on the nav mesh that are near, or next
to, a target, and have some level of confidence that the area will not be too close to other such objects.

For my use case, it's ok if objects path relatively close to one another, but not ok if they end up standing on 
top of each other.


How to use this:

The size of the hex unit is configurable, and the hex map can (and should) be pre-baked into the scene, avoding the 
cost at runtime.

The basic concept is that for any given GameObject, you can create an Occupant within the manager. The occupant maps 
the GameObject's position to a logical hex grid in the map. The Occupant can be given a radius, in order to occupy
an area on the map that is greater than one hex.

If a GameObject would like to path to a location next to another GameObject, the HexGridManager can be queried for a
vacant neighboring hex, which could be used as the NavAgent's destination.

Whenever an Occupant detects that its GameObject has moved to a new grid hexe, the GameObject is sent the 
"OnGridChanged" message, allowing it to take action.


What's here?

A ready-to-use UnityPackage.

Also, this repo contains a sample project with the source code and two scenes: 
1) RandomMovement - a basic demo of many navigating GameObjects and what the grid map looks like as they path around
2) Swarming - a target GameObject is swarmed by many other objects.



Special thanks to this blog post by Red Blob Games for execellent hex math:
http://www.redblobgames.com/grids/hexagons/

And to Baran Kahyaoglu for sample code to generate hex meshes on the fly (for debug visuals)
http://www.barankahyaoglu.com/blog/post/2013/07/26/Hexagons-in-Unity.aspx

TODO:
- Functions to return grid distance between two points
- Function to return best possible grid paths between two points

