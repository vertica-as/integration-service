﻿using System;

namespace Vertica.Integration.Logging.Kibana
{
    public class KibanaConfiguration
    {
        public KibanaConfiguration()
        {
            AzureBlobStorageConnectionStringName = "AzureBlobStorage";
        }

        public KibanaConfiguration Change(Action<KibanaConfiguration> change)
        {
            if (change == null) throw new ArgumentNullException("change");

            change(this);

            return this;
        }

        public string AzureBlobStorageConnectionStringName { get; set; }
    }
}