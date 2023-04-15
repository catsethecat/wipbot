using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wipbot.utils
{
    internal class WebUtils
    {
        private static System.Net.WebClient client = new System.Net.WebClient();

        /**
         * Checks if the map is valid by checking the HTTP status code of the HEAD request.
         * 
         * @param downloadUrl The URL to the map
         * @return true if the map is valid, false otherwise
         */
        public static Boolean IsValidMap(string downloadUrl)
        {
            System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(downloadUrl);
            request.Method = "HEAD";
            request.AllowAutoRedirect = true;
            request.Timeout = 5000;

            try
            {
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }
    }
}
