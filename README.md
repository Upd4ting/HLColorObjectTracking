# HLColorObjectTracking
Hololens library to track object by unique color or LED.

The LED tracking haven't been finished yet. (I don't have anymore access to an Hololens so if someone want to implement tracking he can do a pull request)

This is using these library/plugin:

-	https://github.com/Upd4ting/HoloLensCameraStream
-	https://github.com/Upd4ting/HololensTemplate
-	https://github.com/EnoxSoftware/OpenCVForUnity

# Contributing

You can contribute by forking and doing pull request

# Concept 

Track in real time an object with unique color or LED.
This is using OpenCVForUnity for the computer vision algorithm.

Currently, the Hololens doesn't provide us access to the raw depth data.
Because of that this library can only track object that we have in hand and that we know the size. 
The origin point of this object must also be static (stick for example).

Microsoft is talking about an update to give us access to the information we need.
If this update is released, I will update this library to be able to track any object.

# Documentation

Just put the TrackerManager into your scene and then put on one of your game object the script "ObjectTracker", configure the settings and let's go!
You need to put your AppManifest.xml correctly to be able to use sensors data and you need to enable Research mode.