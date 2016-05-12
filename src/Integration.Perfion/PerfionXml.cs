﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Xml.Linq;
using Vertica.Integration.Infrastructure.Archiving;
using Vertica.Integration.Perfion.Infrastructure;
using Vertica.Utilities_v4.Collections;
using Vertica.Utilities_v4.Extensions.EnumerableExt;

namespace Vertica.Integration.Perfion
{
	public class PerfionXml
	{
		private readonly XDocument _document;

		public PerfionXml(IPerfionService service, XDocument document)
		{
			if (service == null) throw new ArgumentNullException(nameof(service));
			if (document == null) throw new ArgumentNullException(nameof(document));
			if (document.Root == null) throw new ArgumentException(@"Document is missing required root element.");

			Service = service;
			_document = document;
		}

		public IPerfionService Service { get; }

		public XDocument Document => _document;

		public ArchiveCreated Archive { get; internal set; }

		public XElement Root => _document.Root;

		public int Length => _document.ToString().Length;

		public Dictionary<string, Feature> Features()
		{
			var list = new Dictionary<string, Feature>(StringComparer.OrdinalIgnoreCase);

			XElement features = Root.Element("Features");

			if (features != null)
			{
				foreach (var elements in features.Elements().GroupBy(x => x.Name))
				{
					list.Add(elements.Key.LocalName, new Feature(elements));
				}
			}

			return list;
		}

		public IEnumerable<Component> Components(XName name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			return Root.Elements(name).Select(x => new Component(this, x));
		}

		public Tree<Component, int> Tree(XName name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			return Components(name).ToTree(x => x.Id, (x, p) => x.ParentId.HasValue ? p.Value(x.ParentId.Value) : p.None);
		}

		public Tree<Component, TModel, int> Tree<TModel>(XName name, Func<Component, TModel> projection)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (projection == null) throw new ArgumentNullException(nameof(projection));

			return Components(name).ToTree(x => x.Id, (x, p) => x.ParentId.HasValue ? p.Value(x.ParentId.Value) : p.None, projection);
		}

		public override string ToString()
		{
			return _document.ToString();
		}

		public class File
		{
			private readonly PerfionXml _xml;
			private readonly XElement _element;

			public File(PerfionXml xml, XElement element)
			{
				if (xml == null) throw new ArgumentNullException(nameof(xml));
				if (element == null) throw new ArgumentNullException(nameof(element));

				_element = element;
				_xml = xml;
			}

			public XElement Element => _element;

			public Guid Id => _element.AsGuid();

			public string Name => _element.AttributeOrThrow("string").Value;

			public DateTime LastModified => _element.LastModified();

			public DateTime FileLastModified => _element.AttributeOrThrow("fileModifiedDate").AsDateTime();

			public byte[] Download()
			{
				return _xml.Service.DownloadFile(Id);
			}
		}

		public class Image : File
		{
			private readonly PerfionXml _xml;

			public Image(PerfionXml xml, XElement element)
				: base(xml, element)
			{
				_xml = xml;
			}

			public byte[] Download(NameValueCollection options)
			{
				if (options == null) throw new ArgumentNullException(nameof(options));

				return _xml.Service.DownloadImage(Id, options);
			}
		}

		public class Feature
		{
			private readonly IEnumerable<XElement> _elements;

			public Feature(IEnumerable<XElement> elements)
			{
				_elements = elements;
			}

			public string Caption(string language = null)
			{
				return Value("caption", language);
			}

			public string Unit(string language = null)
			{
				return Value("unit", language);
			}

			private string Value(XName attribute, string language)
			{
				return Element(language)?.Attribute(attribute)?.Value;
			}

			private XElement Element(string language)
			{
				return string.IsNullOrWhiteSpace(language)
					? _elements.First()
					: _elements.FirstOrDefault(x => string.Equals(x.Language(), language, StringComparison.OrdinalIgnoreCase));
			}
		}

		public class Component
		{
			private readonly PerfionXml _xml;
			private readonly XElement _element;

			public Component(PerfionXml xml, XElement element)
			{
				if (xml == null) throw new ArgumentNullException(nameof(xml));
				if (element == null) throw new ArgumentNullException(nameof(element));

				_xml = xml;
				_element = element;
			}

			public XElement Element => _element;

			public int Id => _element.Id();

			public int? ParentId => _element.ParentId();

			public string Name(string language = null)
			{
				return this["Value", language];
			}

			public string this[XName name, string language = null]
			{
				get
				{
					if (name == null) throw new ArgumentNullException(nameof(name));

					XElement element = _element.Element(name, language);

					return element?.Value;
				}
			}

			public string[] ValuesFor(XName name, string language = null)
			{
				if (name == null) throw new ArgumentNullException(nameof(name));

				return _element.Elements(name, language).Select(x => x.Value).ToArray();
			}

			public Component Parent
			{
				get
				{
					int? parentId = ParentId;

					if (!parentId.HasValue)
						return null;

					return _xml.Components(_element.Name).SingleOrDefault(x => x.Id == parentId);
				}
			}

			public DateTime LastModified => _element.LastModified();

			public int? IdOf(XName relatedComponent, string language = null)
			{
				if (relatedComponent == null) throw new ArgumentNullException(nameof(relatedComponent));

				XElement element = _element.Element(relatedComponent, language);

				return element?.Id();
			}

			public Component FindRelation(XName relatedComponent, string language = null)
			{
				if (relatedComponent == null) throw new ArgumentNullException(nameof(relatedComponent));

				int? id = IdOf(relatedComponent, language);

				if (!id.HasValue)
					return null;

				return _xml.Components(relatedComponent).FirstOrDefault(x => x.Id == id.Value);
			}

			public File[] GetFiles(XName name)
			{
				if (name == null) throw new ArgumentNullException(nameof(name));

				return _element
					.Elements(name)
					.OrderBy(x => Int32.Parse(x.AttributeOrThrow("seq").Value))
					.Select(x => new File(_xml, x))
					.ToArray();
			}

			public Image[] GetImages(XName name)
			{
				if (name == null) throw new ArgumentNullException(nameof(name));

				return _element
					.Elements(name)
					.OrderBy(x => Int32.Parse(x.AttributeOrThrow("seq").Value))
					.Select(x => new Image(_xml, x))
					.ToArray();
			}


			public int? AsInt32(XName name, string language = null)
			{
				if (name == null) throw new ArgumentNullException(nameof(name));

				string value = this[name, language];

				if (string.IsNullOrWhiteSpace(value))
					return null;

				return Int32.Parse(value);
			}

			public DateTime? AsDateTime(XName name, string language = null)
			{
				if (name == null) throw new ArgumentNullException(nameof(name));

				string value = this[name, language];

				if (string.IsNullOrWhiteSpace(value))
					return null;

				return DateTime.Parse(value, ParsingExtensions.English);
			}

			public double? AsDouble(XName name, string language = null)
			{
				if (name == null) throw new ArgumentNullException(nameof(name));

				string value = this[name, language];

				if (string.IsNullOrWhiteSpace(value))
					return null;

				return Double.Parse(value, ParsingExtensions.English);
			}

			public decimal? AsDecimal(XName name, string language = null)
			{
				if (name == null) throw new ArgumentNullException(nameof(name));

				string value = this[name, language];

				if (string.IsNullOrWhiteSpace(value))
					return null;

				return Decimal.Parse(value, ParsingExtensions.English);
			}
		}
	}
}