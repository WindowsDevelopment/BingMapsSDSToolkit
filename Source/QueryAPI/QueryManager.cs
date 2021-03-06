﻿/*
 * Copyright(c) 2017 Microsoft Corporation. All rights reserved. 
 * 
 * This code is licensed under the MIT License (MIT). 
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
 * of the Software, and to permit persons to whom the Software is furnished to do 
 * so, subject to the following conditions: 
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software. 
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE. 
*/

using BingMapsSDSToolkit.Internal;
using System;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BingMapsSDSToolkit.QueryAPI
{
    /// <summary>
    /// A static class for processing queries.
    /// </summary>
    public static class QueryManager
    {
        #region Public Methods

        /// <summary>
        /// Processes a query request. 
        /// </summary>
        /// <param name="request">A request class that derives from the BaseQueryRequest class.</param>
        /// <returns>A query response.</returns>
        public static async Task<QueryResponse> ProcessQuery(FindByPropertyRequest request)
        {
            return await MakeRequest(request);
        }

        #endregion

        #region Private Methods

        private static async Task<QueryResponse> MakeRequest(FindByPropertyRequest request)
        {
            var result = new QueryResponse();

            try
            {
                string urlRequest = request.GetRequestUrl();
                using (var responseStream = await ServiceHelper.GetStreamAsync(new Uri(urlRequest)))
                {
                    XDocument xmlDoc = XDocument.Load(responseStream);
                    string name;

                    foreach (XElement element in xmlDoc.Descendants(XmlNamespaces.Atom + "entry"))
                    {
                        var r = new QueryResult(){
                            EntityUrl = element.Element(XmlNamespaces.Atom + "id").Value,
                            Location = new GeodataLocation()
                        };

                        XElement content = element.Element(XmlNamespaces.Atom + "content");

                        if (content != null && content.FirstNode != null)
                        {
                            XElement properties = (XElement)content.FirstNode;//.Element(XmlNamespaces.m + "properties");

                            if (properties != null)
                            {                                
                                foreach (var prop in properties.Descendants())
                                {
                                    name = prop.Name.LocalName;

                                    switch (name.ToLowerInvariant())
                                    {
                                        case "latitude":
                                            r.Location.Latitude = XmlUtilities.GetDouble(prop, 0);
                                            break;
                                        case "longitude":
                                            r.Location.Longitude = XmlUtilities.GetDouble(prop, 0);
                                            break;
                                        case "__distance":
                                            r.Distance = SpatialTools.ConvertDistance(XmlUtilities.GetDouble(prop, 0), DistanceUnitType.Kilometers, request.DistanceUnits);
                                            break;
                                        case "__IntersectedGeom":
                                            var wkt = XmlUtilities.GetString(prop);
                                            if (!string.IsNullOrEmpty(wkt))
                                            {
                                                r.IntersectedGeography = new Geography()
                                                {
                                                    WellKnownText = wkt
                                                };
                                            }
                                            break;
                                        default:
                                            if (!r.Properties.ContainsKey(name))
                                            {
                                                var nVal = ParseNodeValue(prop);
                                                r.Properties.Add(name, nVal);

                                                if (nVal is Geography)
                                                {
                                                    r.HasGeography = true;
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                        }

                        result.Results.Add(r);
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static object ParseNodeValue(XElement node)
        {
            if (node.HasAttributes)
            {
                var type = node.Attribute(XmlNamespaces.DataServicesMetadata + "type");
                switch (type.Value)
                {
                    case "Edm.Double":
                        return XmlUtilities.GetDouble(node, 0);
                    case "Edm.Int64":
                        return XmlUtilities.GetInt64(node, 0);
                    case "Edm.Boolean":
                        return XmlUtilities.GetBoolean(node, false);
                    case "Edm.DateTime":
                        return XmlUtilities.GetDateTime(node);
                    case "Edm.Geography":
                        var wkt = XmlUtilities.GetString(node);
                        if (!string.IsNullOrEmpty(wkt))
                        {
                            return new Geography()
                            {
                                WellKnownText = wkt
                            };
                        }

                        return null;
                    case "Edm.String":
                    default:
                        return XmlUtilities.GetString(node);
                }
            }

            return XmlUtilities.GetString(node);
        }

        #endregion
    }
}
