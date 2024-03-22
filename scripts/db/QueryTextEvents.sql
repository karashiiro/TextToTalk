SELECT Timestamp,
       LAST(SPLIT(FIRST(SPLIT(Event._type, ', ')), '.')) AS Type,
       Event.Source AS Source,
       Event.ChatType AS ChatType,
       COALESCE(Event.Speaker, Event.SpeakerName) AS Speaker,
       Event.Text AS Text,
       Event
FROM event
ORDER BY Timestamp DESC;