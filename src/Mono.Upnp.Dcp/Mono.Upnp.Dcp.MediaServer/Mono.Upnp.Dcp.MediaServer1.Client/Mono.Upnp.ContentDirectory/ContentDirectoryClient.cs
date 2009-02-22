// ContentDirectory1.cs auto-generated at 1/17/2009 8:08:36 PM by Sharpener

using System;

using Mono.Upnp.Dcp.MediaServer1;

namespace Mono.Upnp.ContentDirectory
{
    public class ContentDirectoryClient
    {
        readonly UpnpClient client;

        public event EventHandler<DiscoveryEventArgs<ContentDirectory>> ContentDirectoryAdded;

        public ContentDirectoryClient ()
			: this (null)
        {
        }

        public ContentDirectoryClient (UpnpClient client)
        {
            this.client = client ?? new UpnpClient ();
            this.client.ServiceAdded += ClientServiceAdded;
        }

        public UpnpClient Client {
            get { return client; }
        }

        void ClientServiceAdded (object sender, ServiceEventArgs args)
        {
            if (args.Service.Type != ContentDirectory.ServiceType) return;

            try {
				var description = args.Service.GetDescription ();
				if (description != null) {
					var controller = description.GetController ();
					if (controller != null) {
						var service = new ContentDirectory (new ContentDirectoryController (controller));
						OnContentDirectoryAdded (new DiscoveryEventArgs<ContentDirectory> (service));
					}
				}
            } catch {
            }
        }

        public void Browse ()
        {
            client.Browse (ContentDirectory.ServiceType);
        }

        void OnContentDirectoryAdded (DiscoveryEventArgs<ContentDirectory> e)
        {
            var handler = ContentDirectoryAdded;
            if (handler != null) {
                handler (this, e);
            }
        }
    }
}