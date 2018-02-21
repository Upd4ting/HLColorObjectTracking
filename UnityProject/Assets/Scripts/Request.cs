using System.Collections.Generic;
using System.IO;

public class Request {
    public byte[] Image { get; set; }

    public List<ObjectRequest> ORequests { get; set; }

    public struct ObjectRequest {
        public int minH;
        public int maxH;
    }

    public Request(byte[] image) {
        Image = image;
        ORequests = new List<ObjectRequest>();
    }

    public byte[] GetByteArray() {
        MemoryStream ms = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(ms);

        // Number of object requests
        writer.Write(ORequests.Count);
        
        // We write each object requests
        foreach (ObjectRequest or in ORequests) {
            writer.Write(or.minH);
            writer.Write(or.maxH);
        }

        // Size of the picture
        writer.Write(Image.Length);

        // Picture
        writer.Write(Image);

        return ms.ToArray();
    }
}