# HLColorObjectTracking
Hololens library to track object by unique color.

That's not all working yet, once it worked smoothly that will be pushed on the *master* branch.

This is using these library/plugin:

-	https://github.com/Upd4ting/HoloLensCameraStream
-	https://github.com/Upd4ting/HololensTemplate
-	https://github.com/EnoxSoftware/OpenCVForUnity

# Contributing

You can contribute by forking and doing pull request

# Concept 

Track in real time an object with unique color.
This is using OpenCVForUnity for the computer vision algorithm.

We track an objet of a kind of color and we use depth sensor and matrix to get a 3D position based on the camera.

# Documentation

Just put the TrackerManager into your scene and then put on one of your game object the script "ObjectTracker", configure the settings and let's go!
You need to put your AppManifest.xml correctly to be able to use sensors data and you need to enable Research mode.
