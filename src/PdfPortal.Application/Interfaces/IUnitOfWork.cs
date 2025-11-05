using PdfPortal.Domain.Entities;

namespace PdfPortal.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<TemplateRuleSet> TemplateRuleSets { get; }
    IRepository<DocumentOriginal> DocumentOriginals { get; }
    IRepository<DocumentProcessed> DocumentProcessed { get; }
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
