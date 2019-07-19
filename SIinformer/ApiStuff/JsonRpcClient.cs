#region License, Terms and Conditions
//
// Jayrock - A JSON-RPC implementation for the Microsoft .NET Framework
// Written by Atif Aziz (www.raboof.com)
// Copyright (c) Atif Aziz. All rights reserved.
//
// This library is free software; you can redistribute it and/or modify it under
// the terms of the GNU Lesser General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
//
// This library is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
// FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more
// details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with this library; if not, write to the Free Software Foundation, Inc.,
// 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA 
//
// Updated by Andrew Manson
#endregion

using System.Collections.Generic;
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace SIinformer.ApiStuff
{
    
    public class JsonRpcClient
    {
        private int _id;

        public static string DefaultUrl { get; set; }

        public string  Url { get; set; }

        private IWebProxy _proxy = null;

        public JsonRpcClient()
        {
            
        }

        public JsonRpcClient(IWebProxy proxy)
        {
            _proxy = proxy;
        }


        public void Invoke<T>(string method, Action<T> success = null, Action<Exception> fail = null)
        {
             Invoke<T>(method,"", success, fail);
        }


        public void Invoke<T>(RpcCommand command, Action<T> success = null, Action<Exception> fail = null)
        {
             Invoke<T>(command.Method, command.GetJsonParameters(), success, fail);
        }



        public virtual void Invoke<T>(string method, string args, Action<T> success = null, Action<Exception> fail = null) 
        {
            if (method == null) 
                throw new ArgumentNullException("method");
            if (method.Length == 0)
                throw new ArgumentException(null, "method");
        
            try
            {

                var url = string.IsNullOrWhiteSpace(Url) ? DefaultUrl : Url;
                if (string.IsNullOrWhiteSpace(url))
                {
                    if (fail!=null)
                    {
                        fail(new Exception("No Url specified"));
                        return;
                    }else
                        throw new Exception("No Url specified");

                }
                var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
                //request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1;)";
                request.Proxy = _proxy;
                request.ContentType = "text/plain; charset=utf-8";
                request.Method = "POST";

                request.BeginGetRequestStream((target) =>
                                                  {
                                                      HttpWebRequest req=null;
                                                      try
                                                      {
                                                          req = (HttpWebRequest)target.AsyncState;
                                                          using (Stream stream = req.EndGetRequestStream(target))
                                                          using (var writer = new StreamWriter(stream, Encoding.UTF8))
                                                          {
                                                              string data = "{" + string.Format(" \"id\" : \"{0}\",  \"method\" : \"{1}\",  \"params\" : {2} ", (++_id).ToString(), method, args.ToString()) + "}";
                                                              writer.WriteLine(data);
                                                          }
                                                      }
                                                      catch (Exception ex)
                                                      {

                                                          if (fail != null) {fail(ex); return;} else throw ex;
                                                      }

                                                      // GetWebResponse(req,target))
                                                      
                                                                         req.BeginGetResponse(
                                                                              (result) =>
                                                                              {
                                                                                  try
                                                                                  {
                                                                                      var req1 = result.AsyncState as HttpWebRequest;
                                                                                      var resp1 = req1.EndGetResponse(result) as HttpWebResponse;
                                                                                      using (var stream1 = resp1.GetResponseStream())
                                                                                      using (var reader = new StreamReader(stream1, Encoding.UTF8))
                                                                                          if (success != null)
                                                                                          {
                                                                                              try
                                                                                              {
                                                                                                  var data = OnResponse<T>(reader);
                                                                                                  success(data);
                                                                                              }
                                                                                              catch (Exception ex)
                                                                                              {
                                                                                                  if (fail != null) fail(ex); else throw ex;
                                                                                              }
                                                                                          }
                                                                                  }
                                                                                  catch (Exception ex)
                                                                                  {
                                                                                      if (fail != null) fail(ex); else throw ex;
                                                                                  }
                                                                              }, req);

                                                                     }, request);

            }
            catch (Exception ex)
            {
                if (fail != null) fail(ex); else throw ex;
            }
           
        }


        private T OnResponse<T>(StreamReader reader)
        {


            var result = reader.ReadToEnd();
            // fastJSON не умеет конвертить в используемые нами тип, поэтому вместо этого кода
            //var r =  fastJSON.JSON.Instance.ToObject<RpcResult<T>>(result);  , где он валитс€
            // делаем костыль - добал€ем либу Newtonsoft.Json. fastJSON в разы быстрее, но придетс€ все же использовать доп.либу. ¬озможно потом допишу fastJSON, чтобы он работал лучше
            var r = JsonConvert.DeserializeObject<RpcResult<T>>(result);                
            return r.result;
        }

        protected virtual void OnError(object errorObject) 
        {
            var error = errorObject as IDictionary;
                        
            if (error != null)
                throw new Exception(error["message"] as string);
                        
            throw new Exception(errorObject as string);
        }
    }

   
   public class RpcResult<T>
   {
       public int id { get; set; }
       public T result { get; set; }
   }
 
    public class RpcCommand
    {
        public string Method { get; set; }
        public Dictionary<string,string> Parameters { get; private set; }
        public object[] ParametersArray { set { ProcessParametersArray(value); } }

        private void ProcessParametersArray(object[] values)
        {
            if (values.Length  % 2 !=0) throw new Exception("Ќеверное количество параметров (должно быть [им€ : значение], то есть кратными двум)");
            for (int i = 0; i < values.Length /2; i++)
            {
                var index = i*2;
                AddParameter(values[index].ToString(), values[index+1]);
            }
        }

        public RpcCommand()
        {
            Parameters = new Dictionary<string, string>();
        }
        public void AddParameter(string name, object parameter)
        {
            var paramJson = new fastJSON.JSONParameters
                                {
                                    UsingGlobalTypes = false,
                                    EnableAnonymousTypes = true,
                                    UseExtensions = false
                                };

            Parameters.Add(name, fastJSON.JSON.Instance.ToJSON(parameter, paramJson));
        }
        public string GetJsonParameters()
        {
            var result = "";
            foreach (var parameter in Parameters)
            {
                if (!string.IsNullOrWhiteSpace(result)) result += ",";
                result += string.Format(" \"{0}\" : {1}", parameter.Key, parameter.Value);
            }
            result = "{" + result + "}";
            return result;
        }
    }
  
}
