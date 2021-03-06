﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Xml;
using System.Xml.XPath;

using umbraco;
using umbraco.BusinessLogic;
using umbraco.NodeFactory;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

using Vega.USiteBuilder.Types;

namespace Vega.USiteBuilder
{
    /// <summary>
    /// This class contains methods for getting the strongly typed content from Umbraco
    /// </summary>
    public static class ContentHelper
    {
        /// <summary>
        /// Contains list of all custom type convertors.
        /// </summary>
        internal static Dictionary<Type, ICustomTypeConvertor> PropertyConvertors = new Dictionary<Type, ICustomTypeConvertor>();
        static readonly IContentService ContentService = ApplicationContext.Current.Services.ContentService;

        /// <summary>
        /// Registers document type property convertor.
        /// </summary>
        /// <param name="propertyType">Document type property type</param>
        /// <param name="convertor">Convertor implementation</param>
        internal static void RegisterDocumentTypePropertyConvertor(Type propertyType, ICustomTypeConvertor convertor)
        {
            if (!PropertyConvertors.ContainsKey(propertyType))
            {
                PropertyConvertors.Add(propertyType, convertor);
            }
        }

        /// <summary>
        /// Gets the current content being rendered.
        /// </summary>
        /// <returns></returns>
        public static DocumentTypeBase GetCurrentContent()
        {
            return DocumentTypeResolver.Instance.GetTyped<DocumentTypeBase>(Node.GetCurrent());
        }

        /// <summary>
        /// Gets all children nodes of a given type from a given node id.
        /// </summary>
        /// <typeparam name="T">Strongly typed content item</typeparam>
        /// <param name="parentId">Parent node id of all children to get</param>
        /// <param name="deepGet">If true it does deep search for children in the whole content tree starting from node whose id is parentId)</param>
        public static IEnumerable<T> GetChildren<T>(int parentId, bool deepGet)
            where T : DocumentTypeBase, new()
        {
            Node parentNode = uQuery.GetNode(parentId);            

            string docTypeAlias = DocumentTypeManager.GetDocumentTypeAlias(typeof(T));

            IEnumerable<Node> childNodes = null;
            if (deepGet)
            {
                childNodes = parentNode.GetDescendantNodes();
            }
            else
            {
                childNodes = parentNode.GetChildNodes();
            }

            foreach (Node childNode in childNodes)
            {
                // Check if this childNode is of a given document type and if not deleted
                if (docTypeAlias == childNode.NodeTypeAlias && !ContentHelper.IsInRecycleBin(childNode.Path))
                {
                    var d = ContentHelper.GetByNode<T>(childNode);
                    if (d != null)
                    {
                        yield return d;
                    }
                }
            }
        }

        /// <summary>
        /// Gets all children nodes of a given type from a given node id.
        /// Note: This method returns only first level children - it doesn't return children's children.
        /// </summary>
        /// <typeparam name="T">Strongly typed content item</typeparam>
        /// <param name="parentId">Parent node id of all children to get</param>
        public static IEnumerable<T> GetChildren<T>(int parentId)
            where T : DocumentTypeBase, new()
        {
            return ContentHelper.GetChildren<T>(parentId, false);
        }

        /// <summary>
        /// Gets all children nodes from a given node id.
        /// Note: This method returns only first level children - it doesn't return children's children.
        /// </summary>
        /// <param name="parentId">Parent node id of all children to get</param>
        public static IEnumerable<DocumentTypeBase> GetChildren(int parentId)
        {
            return ContentHelper.GetChildren(parentId, false);
        }

        /// <summary>
        /// Gets all children nodes from a given node id.
        /// </summary>
        /// <param name="parentId">Parent node id of all children to get</param>
        /// <param name="deepGet">if set to <c>true</c> method will return children's children (complete tree).</param>
        /// <returns></returns>
        public static IEnumerable<DocumentTypeBase> GetChildren(int parentId, bool deepGet)
        {
            Node parentNode = uQuery.GetNode(parentId);

            if (parentNode.Id == parentId) // check if it is loaded correctly
            {
                IEnumerable<Node> childNodes = null;
                if (deepGet)
                {
                    childNodes = parentNode.GetDescendantNodes();
                }
                else
                {
                    childNodes = parentNode.GetChildNodes();
                }

                if (childNodes != null)
                {
                    foreach (Node childNode in childNodes)
                    {
                        // Check if this childNode is not deleted
                        if (!ContentHelper.IsInRecycleBin(childNode.Path))
                        {
                            var d = DocumentTypeResolver.Instance.GetTyped<DocumentTypeBase>(childNode);
                            if (d != null)
                            {
                                yield return d;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get's the content item by node id.
        /// </summary>
        /// <typeparam name="T">Strongly typed content item</typeparam>
        /// <param name="nodeId">Node Id associated with the content item</param>
        /// <returns>Content item</returns>
        public static T GetByNodeId<T>(int nodeId)
            where T : DocumentTypeBase, new()
        {
            return DocumentTypeResolver.Instance.GetTyped<T>(nodeId);
        }


        /// <summary>
        /// Get's the content item by node.
        /// </summary>
        /// <typeparam name="T">Strongly typed content item</typeparam>
        /// <param name="node">node associated with the content item</param>
        /// <returns>Content item</returns>
        public static T GetByNode<T>(Node node)
             where T : DocumentTypeBase, new()
        {
            return DocumentTypeResolver.Instance.GetTyped<T>(node);
        }

        /// <summary>
        /// Get's the content item by node id.
        /// </summary>
        /// <param name="nodeId">Node Id associated with the content item</param>
        /// <returns>Content item</returns>
        public static DocumentTypeBase GetByNodeId(int nodeId)
        {
            return DocumentTypeResolver.Instance.GetTyped<DocumentTypeBase>(nodeId);
        }

        /// <summary>
        /// Get's the content item by node.
        /// </summary>
        /// <param name="node">node associated with the content item</param>
        /// <returns>Content item</returns>
        public static DocumentTypeBase GetByNode(Node node)
        {
            return DocumentTypeResolver.Instance.GetTyped<DocumentTypeBase>(node);
        }

        internal static object GetPropertyValue(DocumentTypeBase entity, PropertyInfo propInfo)
        {
            return GetPropertyValue(entity, propInfo, null);
        }

        internal static object GetPropertyValue(DocumentTypeBase entity, PropertyInfo propInfo, DocumentTypePropertyAttribute propAttr)
        {
            string propertyName;
            string propertyAlias;
            object value = null;

            if (propAttr == null)
            {
                propAttr = Util.GetAttribute<DocumentTypePropertyAttribute>(propInfo);
            }

            DocumentTypeManager.ReadPropertyNameAndAlias(propInfo, propAttr, out propertyName, out propertyAlias);

            var property = entity.Source.GetProperty(propertyAlias);


            if (property == null)
            {
                value = null;
            }

            else if (propInfo.PropertyType.Equals(typeof(Boolean)))
            {
                if (String.IsNullOrEmpty(property.Value) || property.Value == "0")
                {
                    value = false;
                }
                else
                {
                    value = true;
                }
            }
            else if (propAttr.CustomTypeConverter != null)
            {
                value = ((ICustomTypeConvertor)Activator.CreateInstance(propAttr.CustomTypeConverter)).ConvertValueWhenRead(property.Value);
            }
            else if (ContentHelper.PropertyConvertors.ContainsKey(propInfo.PropertyType))
            {
                // will be transformed later. TODO: move transformation here
                //value = ContentHelper.GetInnerXml(node.Id.ToString(), propertyAlias);

                value = PropertyConvertors[propInfo.PropertyType].ConvertValueWhenRead(property.Value);

            }
            else if (String.IsNullOrEmpty(property.Value))
            {
                // if property type is string or if it's some custom type, try to get the inner xml of this property within a node.
                if (propInfo.PropertyType == typeof(string) ||
                    ContentHelper.PropertyConvertors.ContainsKey(propInfo.PropertyType))
                {
                    value = ContentHelper.GetInnerXml(entity.Source.Id.ToString(), propertyAlias);
                    if (value == null && propInfo.PropertyType == typeof(string))
                    {
                        value = string.Empty;
                    }
                }
                else
                {
                    value = null;
                }
            }
            else if (propInfo.PropertyType.IsGenericType &&
                     propInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                value = Convert.ChangeType(property.Value, Nullable.GetUnderlyingType(propInfo.PropertyType));

                // TODO: If data type is DateTime and is nullable and is less than 1.1.1000 than set it to NULL
            }
            else if (propInfo.PropertyType.Equals(typeof(HtmlString)))
            {
                value = new HtmlString(property.Value);
            }
            else
            {
                value = Convert.ChangeType(property.Value, propInfo.PropertyType);
            }

            return value;
        }

        private static string GetInnerXml(string nodeId, string propertyAlias)
        {
            string retVal = null;

            XmlNode node = content.Instance.XmlContent.GetElementById(nodeId);
            if (node != null)
            {
                XmlNode propertyNode = node.SelectSingleNode(propertyAlias);
                if (propertyNode != null && propertyNode.FirstChild != null)
                {
                    if (propertyNode.FirstChild.GetType() != typeof(XmlCDataSection))
                    {
                        retVal = propertyNode.InnerXml;
                    }
                    else
                    {
                        retVal = propertyNode.InnerText;
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets the content by XPath query
        /// </summary>
        /// <param name="xpath"></param>
        /// <returns>List of content that matches the XPath query</returns>
        public static List<DocumentTypeBase> SelectContentNodes(string xpath)
        {
            List<DocumentTypeBase> retVal = new List<DocumentTypeBase>();

            XPathNodeIterator rootDocIterator = umbraco.library.GetXmlAll();

            XmlDocument rootDoc = new XmlDocument();
            rootDoc.LoadXml(rootDocIterator.Current.OuterXml);

            XmlNodeList nodes = rootDoc.SelectNodes(xpath);

            foreach (XmlNode node in nodes)
            {
                Node n = new Node(node);

                var d = DocumentTypeResolver.Instance.GetTyped<DocumentTypeBase>(n);
                if (d != null)
                {
                    retVal.Add(d);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Updates or adds the content item using current user. If content item already exists, it updates it.
        /// If content item doesn't exists, it creates new content item.
        /// NOTE: Set the ParentId property of this item.
        /// </summary>
        /// <param name="contentItem">Content item to update/add</param>
        /// <param name="publish">If set to <c>true</c> it contentItem will be published as well.</param>
        public static void Save(DocumentTypeBase contentItem, bool publish)
        {
            ContentHelper.Save(contentItem, Util.GetAdminUser(), publish);
        }

        /// <summary>
        /// Updates or adds the content item using current user. If content item already exists, it updates it. 
        /// If content item doesn't exists, it creates new content item.
        /// NOTE: Set the ParentId property of this item.
        /// </summary>
        /// <param name="contentItem">Content item to update/add</param>
        public static void Save(DocumentTypeBase contentItem)
        {
            ContentHelper.Save(contentItem, Util.GetAdminUser(), true);
        }

        /// <summary>
        /// Updates or adds the content item. If content item already exists, it updates it. 
        /// If content item doesn't exists, it creates new content item (in that case contentItem.Id will be set to newly created id).
        /// NOTE: Set the ParentId property of this item.
        /// </summary>
        /// <param name="contentItem">Content item to update/add</param>
        /// <param name="user">User used for add or updating the content</param>
        /// <param name="publish">If set to <c>true</c> it contentItem will be published as well.</param>
        public static void Save(DocumentTypeBase contentItem, User user, bool publish)
        {
            if (user == null)
            {
                throw new Exception("User cannot be null");
            }

            if (contentItem.ParentId < 1)
            {
                throw new ArgumentException("Parent property cannot be null");
            }

            if (String.IsNullOrEmpty(contentItem.Name))
            {
                throw new Exception("Name property of this content item is not set");
            }

            IContentType contentType = DocumentTypeManager.GetDocumentType(contentItem.GetType());

            IContent content;
            if (contentItem.Id == 0) // content item is new so create Document
            {
                content = ContentService.CreateContent(contentItem.Name, contentItem.ParentId, contentType.Alias);
            }
            else // content item already exists, so load it
            {
                content = ContentService.GetById(contentItem.Id);
            }

            foreach (PropertyInfo propInfo in contentItem.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    DocumentTypePropertyAttribute propAttr = Util.GetAttribute<DocumentTypePropertyAttribute>(propInfo);
                    if (propAttr == null)
                    {
                        continue; // skip this property - not part of a Document Type
                    }

                    string propertyName;
                    string propertyAlias;
                    DocumentTypeManager.ReadPropertyNameAndAlias(propInfo, propAttr, out propertyName, out propertyAlias);

                    PropertyType property = content.PropertyTypes.FirstOrDefault(p => p.Alias == propertyAlias);
                    if (property == null)
                    {
                        throw new Exception(string.Format("Property '{0}' not found in this node: {1}. Content type: {2}.",
                            propertyAlias, content.Id, contentType.Alias));
                    }

                    if (PropertyConvertors.ContainsKey(propInfo.PropertyType))
                    {
                        content.SetValue(propertyAlias, PropertyConvertors[propInfo.PropertyType].ConvertValueWhenWrite(propInfo.GetValue(contentItem, null)));
                    }
                    else
                    {
                        content.SetValue(propertyAlias, propInfo.GetValue(contentItem, null));
                    }
                }
                catch (Exception exc)
                {
                    throw new Exception(String.Format("Error while saving property: {0}.{1}. Error: {2}, Stack trace: {3}", contentType.Alias, propInfo.Name, exc.Message, exc.StackTrace), exc);
                }
            }

            if (publish)
            {
                ContentService.SaveAndPublish(content);
                contentItem.Id = content.Id;
            }
        }

        /// <summary>
        /// Deletes the content specified by id.
        /// </summary>
        /// <param name="id">The node id to delete.</param>
        /// <param name="deletePermanently">if set to <c>true</c>, node will be deleted without moving to Trash (otherwise items is moved to Trash).</param>
        public static void DeleteContent(int id, bool deletePermanently)
        {
            ContentService.Delete(ContentService.GetById(id));
        }

        /// <summary>
        /// Deletes the content specified by id (moves items to Trash)
        /// </summary>
        /// <param name="id">Content item id</param>
        public static void DeleteContent(int id)
        {
            DeleteContent(id, false);
        }

        /// <summary>
        /// Returns true if content item is deleted and currently contained in the recycle bin.
        /// </summary>
        /// <param name="contentItem">Content item</param>
        public static bool IsInRecycleBin(DocumentTypeBase contentItem)
        {
            return contentItem.Path.Contains(string.Format(",{0},", Constants.UmbracoRecycleBinId));
        }

        /// <summary>
        /// Checks if node with a given path is in the Recycle bin
        /// </summary>
        /// <param name="path">Node path</param>
        /// <returns>true if in recycle bin</returns>
        public static bool IsInRecycleBin(string path)
        {
            return path.Contains(string.Format(",{0},", Constants.UmbracoRecycleBinId));
        }

        internal static bool PopuplateInstance<T>(Node node, Type typeDocType, T typedPage) where T : DocumentTypeBase
        {
            if (node == null || node.NodeTypeAlias == null || node.Id == 0)
            {
                return false;
            }

            int parentId = 0;
            try
            {
                // it is required to put this to try catch because it is not possible to check if node.Parent is empty.
                parentId = node.Parent.Id;
            }
            catch { }

            typedPage.Id = node.Id;
            typedPage.Name = node.Name;
            typedPage.ParentId = parentId;
            typedPage.CreateDate = node.CreateDate;
            typedPage.UpdateDate = node.UpdateDate;
            typedPage.CreatorId = node.CreatorID;
            typedPage.CreatorName = node.CreatorName;
            typedPage.NiceUrl = node.NiceUrl;
            typedPage.NodeTypeAlias = node.NodeTypeAlias;
            typedPage.Path = node.Path;
            typedPage.SortOrder = node.SortOrder;
            typedPage.Template = node.template;
            typedPage.Url = node.Url;
            typedPage.WriterID = node.WriterID;
            typedPage.WriterName = node.WriterName;
            typedPage.Version = node.Version;
            typedPage.Source = node;

            foreach (PropertyInfo propInfo in typeDocType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                DocumentTypePropertyAttribute propAttr = Util.GetAttribute<DocumentTypePropertyAttribute>(propInfo);
                if (propAttr == null || (propInfo.GetGetMethod() != null && propInfo.GetGetMethod().IsVirtual && !propInfo.GetGetMethod().IsFinal))
                {
                    continue; // skip this property - not part of a Document Type or is virtual in which case value will be intercepted
                }

                object value = null;
                try
                {
                    value = GetPropertyValue(typedPage, propInfo, propAttr);
                    propInfo.SetValue(typedPage, value, null);
                }
                catch (Exception exc)
                {
                    throw new Exception(string.Format("Cannot set the value of a document type property {0}.{1} (document type: {2}) to value: '{3}' (value type: {4}). Error: {5}",
                        typeDocType.Name, propInfo.Name, propInfo.PropertyType.FullName,
                        value, value != null ? value.GetType().FullName : "", exc.Message));
                }
            }

            return true;
        }
    }
}
