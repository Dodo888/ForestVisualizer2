using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestVisualizer
{
    class Serializer
    {
        public MemoryStream Serialize(object obj)
        {
            MemoryStream ms = new MemoryStream();
            using (StreamWriter sw = new StreamWriter(ms))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, obj);
            }
            return ms;
        }

        public T Deserialize<T>(MemoryStream ms)
        {
            T obj;
            using (StreamReader sw = new StreamReader(ms))
            using (JsonReader reader = new JsonTextReader(sw))
            {
                JsonSerializer serializer = new JsonSerializer();

                obj = serializer.Deserialize<T>(reader);
            }
            return obj;
        }

        static public void Write<T>(T obj, Stream stream)
        {
            var serializer = new JsonSerializer();
            var writer = new JsonTextWriter(new StreamWriter(stream));
            try
            {
                serializer.Serialize(writer, obj);
                writer.Flush();
            }
            catch (Exception e)
            {
                //                Console.WriteLine(e);
                writer.Close();
                throw;
            }
        }

        static public T Read<T>(Stream stream, int timeout = 2000) where T : class
        {
            var serializer = new JsonSerializer();
            var streamReader = new StreamReader(stream);
            streamReader.BaseStream.ReadTimeout = timeout;
            var reader = new JsonTextReader(streamReader);
            T obj = null;
            try
            {
                obj = serializer.Deserialize<T>(reader);
            }
            catch (Exception e)
            {
                //                Console.WriteLine(e);
                reader.Close();
                return null;
                throw;
            }
            return obj;
        }
    }
}
