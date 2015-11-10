using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Utilities_v4.Extensions.StringExt;

namespace Vertica.Integration.Infrastructure.Configuration
{
	internal class FileBasedConfigurationRepository : IConfigurationRepository
	{
		private readonly string _baseDirectory;
		private readonly ILogger _logger;

		public const string BaseDirectoryKey = "FileBasedConfigurationRepository.BaseDirectory";

		public FileBasedConfigurationRepository(IRuntimeSettings settings, ILogger logger)
		{
			_baseDirectory = settings[BaseDirectoryKey].NullIfEmpty() ?? "Data\\Configurations";

			if (!Directory.Exists(_baseDirectory))
				Directory.CreateDirectory(_baseDirectory);

			_logger = logger;
		}

		public Configuration[] GetAll()
		{
			return new DirectoryInfo(_baseDirectory)
				.EnumerateFiles("*.json")
				.Select(Map)
				.ToArray();
		}

		public Configuration Get(string id)
		{
			if (String.IsNullOrWhiteSpace(id)) throw new ArgumentException(@"Value cannot be null or empty.", "id");

			FileInfo configurationFile = ConfigurationFilePath(id);

			if (!configurationFile.Exists)
				return null;

			return Map(configurationFile);
		}

		public Configuration Save(Configuration configuration)
		{
			if (configuration == null) throw new ArgumentNullException("configuration");

			FileInfo configurationFile = ConfigurationFilePath(configuration.Id);

			File.WriteAllText(configurationFile.FullName, configuration.JsonData);
			File.WriteAllText(MetaFilePath(configurationFile).FullName, new MetaFile(configuration).ToString());

			return Get(configuration.Id);
		}

		public void Delete(string id)
		{
			if (String.IsNullOrWhiteSpace(id)) throw new ArgumentException(@"Value cannot be null or empty.", "id");

			FileInfo configurationFile = ConfigurationFilePath(id);

			if (configurationFile.Exists)
			{
				FileInfo metaFile = MetaFilePath(configurationFile);

				configurationFile.Delete();

				if (metaFile.Exists)
					metaFile.Delete();				
			}
		}

		private FileInfo ConfigurationFilePath(string id)
		{
			return new FileInfo(Path.Combine(_baseDirectory, String.Format("{0}.json", id)));
		}

		private Configuration Map(FileInfo configurationFile)
		{
			MetaFile metaFile = ReadMetaFile(configurationFile);

			return new Configuration
			{
				Id = Path.GetFileNameWithoutExtension(configurationFile.Name),
				JsonData = File.ReadAllText(configurationFile.FullName),
				Created = configurationFile.CreationTimeUtc,
				Updated = configurationFile.LastWriteTimeUtc,
				Name = metaFile != null ? metaFile.Name : configurationFile.Name,
				Description = metaFile != null ? metaFile.Description : null,
				UpdatedBy = metaFile != null ? metaFile.UpdatedBy : null
			};
		}

		private MetaFile ReadMetaFile(FileInfo archiveFile)
		{
			FileInfo filePath = MetaFilePath(archiveFile);

			if (!filePath.Exists)
				return null;

			string text = File.ReadAllText(filePath.FullName);

			if (String.IsNullOrWhiteSpace(text))
				return null;

			return MetaFile.FromJson(text, _logger);
		}

		private static FileInfo MetaFilePath(FileInfo archiveFile)
		{
			return new FileInfo(Path.Combine(String.Format("{0}.meta", archiveFile.FullName)));
		}

		private class MetaFile
		{
			public MetaFile(Configuration configuration = null)
			{
				if (configuration != null)
				{
					Name = configuration.Name;
					Description = configuration.Description;
					UpdatedBy = configuration.UpdatedBy;
				}
			}

			public string Name { get; set; }
			public string Description { get; set; }
			public string UpdatedBy { get; set; }

			public override string ToString()
			{
				return JsonConvert.SerializeObject(this, Formatting.Indented);
			}

			public static MetaFile FromJson(string json, ILogger logger)
			{
				if (String.IsNullOrWhiteSpace(json)) throw new ArgumentException(@"Value cannot be null or empty.", "json");
				if (logger == null) throw new ArgumentNullException("logger");

				try
				{
					return JsonConvert.DeserializeAnonymousType(json, new MetaFile());
				}
				catch (Exception ex)
				{
					logger.LogError(ex);

					return null;
				}
			}
		}
	}
}