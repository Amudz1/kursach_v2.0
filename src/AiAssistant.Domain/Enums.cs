namespace AiAssistant.Domain;

public enum Role { User, Admin }
public enum MemorySource { Manual, Auto }
public enum MessageRole { User, Assistant, System }
public enum GenerationStatus { Completed, Pending, Streaming, Stopped, Failed, Superseded }
public enum SubscriptionStatus { Active, Replaced, Expired }
public enum TransactionStatus { Completed }
