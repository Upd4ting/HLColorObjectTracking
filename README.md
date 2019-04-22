# HLColorObjectTracking
Hololens library to track object by unique color. 

This is using these library/plugin:

-	https://github.com/Upd4ting/HoloLensCameraStream
-	https://github.com/Upd4ting/HololensTemplate
-	https://github.com/EnoxSoftware/OpenCVForUnity

So don't forget to clone *recursive*!

# Contributing

You can contribute by forking and doing pull request

# Concept 

Track in real time an object with unique color (led, painted object in a color, etc).
This is using OpenCVForUnity for the computer vision algorithm.

Currently, the Hololens doesn't provide us access to the raw depth data.
Because of that this library can only track object that we have in hand and that we know the size. 
The origin point of this object must also be static (stick for example).

Microsoft is talking about an update to give us access to the information we need.
If this update is released, I will update this library to be able to track any object.

**EDIT:** Since april we got an insider update on the Hololens that allows use to catch raw data from the depth sensor. You can see a start of the implementation in the branch *useDepth* but that's not all working yet, feel free to help me finishing that as I got less time to finish this library

# Documentation

Coming soon...
