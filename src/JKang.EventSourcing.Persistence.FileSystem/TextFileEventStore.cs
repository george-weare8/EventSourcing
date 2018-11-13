﻿using JKang.EventSourcing.Events;
using JKang.EventSourcing.Serialization;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JKang.EventSourcing.Persistence.FileSystem
{
    public class TextFileEventStore : IEventStore
    {
        private readonly IOptions<TextFileEventStoreOptions> _options;
        private readonly IEventSerializer _eventSerializer;

        public TextFileEventStore(
            IOptions<TextFileEventStoreOptions> options,
            IEventSerializer eventSerializer)
        {
            _options = options;
            _eventSerializer = eventSerializer;
        }

        public async Task AddEventAsync(string aggregateType, AggregateEvent @event)
        {
            string serialized = _eventSerializer.Serialize(@event);
            string filePath = GetAggregateFilePath(aggregateType, @event.AggregateId, createFolderIfNotExist: true);
            using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs))
            {
                if (fs.Position > 0)
                {
                    await sw.WriteAsync(_options.Value.EventSeparator);
                }
                await sw.WriteAsync(serialized);
            }
        }

        public Task<Guid[]> GetAggregateIdsAsync(string aggregateType)
        {
            return Task.Run(() =>
            {
                string folder = GetAggregateFolder(aggregateType);
                var di = new DirectoryInfo(folder);
                if (!di.Exists)
                {
                    return new Guid[0];
                }

                return di.GetFiles("*.txt", SearchOption.TopDirectoryOnly)
                    .Select(x => x.Name)
                    .Select(x => Path.GetFileNameWithoutExtension(x))
                    .Select(x => Guid.Parse(x))
                    .ToArray();
            });
        }

        public async Task<AggregateEvent[]> GetEventsAsync(string aggregateType, Guid aggregateId)
        {
            string filePath = GetAggregateFilePath(aggregateType, aggregateId);
            if (!File.Exists(filePath))
            {
                return new AggregateEvent[0];
            }

            var events = new List<AggregateEvent>();
            string text;
            using (FileStream fs = File.OpenRead(filePath))
            using (var sr = new StreamReader(fs))
            {
                text = await sr.ReadToEndAsync();
            }

            return text.Split(new[] { _options.Value.EventSeparator }, StringSplitOptions.None)
                .Select(x => _eventSerializer.Deserialize(x) as AggregateEvent)
                .ToArray();
        }

        private string GetAggregateFilePath(string aggregateType, Guid aggregateId, bool createFolderIfNotExist = false)
        {
            string folder = GetAggregateFolder(aggregateType, createFolderIfNotExist);
            return Path.Combine(folder, $"{aggregateId}.txt");
        }

        private string GetAggregateFolder(string aggregateType, bool createIfNotExist = false)
        {
            string folder = Path.Combine(_options.Value.Folder, aggregateType);
            if (createIfNotExist)
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }
    }
}