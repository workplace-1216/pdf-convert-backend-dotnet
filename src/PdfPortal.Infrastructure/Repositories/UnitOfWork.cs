using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PdfPortal.Application.Interfaces;
using PdfPortal.Domain.Entities;
using PdfPortal.Infrastructure.Data;

namespace PdfPortal.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PdfPortalDbContext _context;
    private readonly IRepository<User> _users;
    private readonly IRepository<TemplateRuleSet> _templateRuleSets;
    private readonly IRepository<DocumentOriginal> _documentOriginals;
    private readonly IRepository<DocumentProcessed> _documentProcessed;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(PdfPortalDbContext context)
    {
        _context = context;
        _users = new Repository<User>(_context);
        _templateRuleSets = new Repository<TemplateRuleSet>(_context);
        _documentOriginals = new Repository<DocumentOriginal>(_context);
        _documentProcessed = new Repository<DocumentProcessed>(_context);
    }

    public IRepository<User> Users => _users;
    public IRepository<TemplateRuleSet> TemplateRuleSets => _templateRuleSets;
    public IRepository<DocumentOriginal> DocumentOriginals => _documentOriginals;
    public IRepository<DocumentProcessed> DocumentProcessed => _documentProcessed;

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
