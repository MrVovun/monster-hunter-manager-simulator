using System;
using UnityEngine;

[Serializable]
public class NotificationMessageTemplate
{
    public NotificationSeverity severity = NotificationSeverity.Info;
    public string title;
    [TextArea(1, 3)] public string body;

    public NotificationMessageTemplate() { }

    public NotificationMessageTemplate(NotificationSeverity severity, string title, string body)
    {
        this.severity = severity;
        this.title = title;
        this.body = body;
    }
}
