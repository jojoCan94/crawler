using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GooglePlayStoreCrawler
{
    public class BufferManager
    {
        private int maxBufferSize;
        private StringBuilder content;

        public BufferManager(int maxBufferSize)
        {
            this.maxBufferSize = maxBufferSize;
            this.content = new StringBuilder();
        }

        public async Task ReadStreamAsync(Stream stream)
        {
            byte[] buffer = new byte[4096];
            int bytesRead = 0;

            while (content.Length < maxBufferSize && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                content.Append(chunk);
            }
        }

        public string GetBufferedContent()
        {
            return content.ToString();
        }
    }
}
