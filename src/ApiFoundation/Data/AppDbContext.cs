using ApiFoundation.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<LinkArquivo> Links => Set<LinkArquivo>();
    public DbSet<NotificacaoLeomob> Notificacoes => Set<NotificacaoLeomob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>(pedido =>
        {
            pedido.HasIndex(p => p.PedidoSVL).IsUnique();
            pedido.Property(p => p.TipoPedido).HasConversion<string>();

            pedido.HasOne(p => p.Notificacao)
                  .WithOne()
                  .HasForeignKey<NotificacaoLeomob>(n => n.PedidoId);

            pedido.HasData(
                new Pedido { Id = 1, PedidoSVL = 1007, ServiceId = 21763653, TipoPedido = TipoPedido.ZLPL },
                new Pedido { Id = 2, PedidoSVL = 1008, ServiceId = 21763654, TipoPedido = TipoPedido.ZCVS },
                new Pedido { Id = 3, PedidoSVL = 1012, ServiceId = 21763699, TipoPedido = TipoPedido.ZMOB });
        });

        modelBuilder.Entity<LinkArquivo>()
            .HasIndex(l => new { l.PedidoId, l.Url })
            .IsUnique();

        modelBuilder.Entity<NotificacaoLeomob>()
            .Property(n => n.Status)
            .HasConversion<string>();
    }
}