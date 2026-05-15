using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanejaAi.Web.Migrations
{
    /// <inheritdoc />
    public partial class mudançadecoluna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "checklist",
                columns: table => new
                {
                    check_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    even_id = table.Column<int>(type: "int", nullable: false),
                    check_descricao = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    check_concluido = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checklist", x => x.check_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "convidados",
                columns: table => new
                {
                    conv_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    even_id = table.Column<int>(type: "int", nullable: false),
                    conv_nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    conv_documento = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    conv_token = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    conv_confirmacao = table.Column<int>(type: "int", nullable: false),
                    conv_observacoes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_convidados", x => x.conv_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "empresas",
                columns: table => new
                {
                    emp_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    emp_nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_telefone = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_cnpj = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_data_cadastro = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    emp_status = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_cep = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_endereco = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_numero = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_bairro = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_cidade = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_estado = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresas", x => x.emp_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "logs",
                columns: table => new
                {
                    log_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    log_acao = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_tabela = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_descricao = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_usuario = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_ip = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_data = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    emp_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logs", x => x.log_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "categorias_servico",
                columns: table => new
                {
                    cat_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    emp_id = table.Column<int>(type: "int", nullable: false),
                    cat_nome = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cat_data_cadastro = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias_servico", x => x.cat_id);
                    table.ForeignKey(
                        name: "FK_categorias_servico_empresas_emp_id",
                        column: x => x.emp_id,
                        principalTable: "empresas",
                        principalColumn: "emp_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    clien_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    emp_id = table.Column<int>(type: "int", nullable: false),
                    clien_nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_documento = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_telefone = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_cep = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_logradouro = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_numero = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_bairro = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_cidade = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_uf = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clien_status = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    clien_data = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.clien_id);
                    table.ForeignKey(
                        name: "FK_clientes_empresas_emp_id",
                        column: x => x.emp_id,
                        principalTable: "empresas",
                        principalColumn: "emp_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    forn_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    forn_nome = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    forn_cnpj_cpf = table.Column<string>(type: "varchar(18)", maxLength: 18, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    forn_email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    forn_telefone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    forn_observacao = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    forn_status = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    forn_data_cadastro = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    emp_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fornecedores", x => x.forn_id);
                    table.ForeignKey(
                        name: "FK_fornecedores_empresas_emp_id",
                        column: x => x.emp_id,
                        principalTable: "empresas",
                        principalColumn: "emp_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "funcionarios",
                columns: table => new
                {
                    func_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    func_nome = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    func_email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    func_cpf = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    func_cargo = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funcionarios", x => x.func_id);
                    table.ForeignKey(
                        name: "FK_funcionarios_empresas_emp_id",
                        column: x => x.emp_id,
                        principalTable: "empresas",
                        principalColumn: "emp_id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "produtos_fornecedor",
                columns: table => new
                {
                    prod_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    forn_id = table.Column<int>(type: "int", nullable: false),
                    cat_id = table.Column<int>(type: "int", nullable: false),
                    prod_nome = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    prod_descricao = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    prod_valor_padrao = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    prod_unidade = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    prod_ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    prod_data_cadastro = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos_fornecedor", x => x.prod_id);
                    table.ForeignKey(
                        name: "FK_produtos_fornecedor_categorias_servico_cat_id",
                        column: x => x.cat_id,
                        principalTable: "categorias_servico",
                        principalColumn: "cat_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_produtos_fornecedor_fornecedores_forn_id",
                        column: x => x.forn_id,
                        principalTable: "fornecedores",
                        principalColumn: "forn_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "login",
                columns: table => new
                {
                    login_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    func_id = table.Column<int>(type: "int", nullable: true),
                    login_email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    login_senha = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emp_id = table.Column<int>(type: "int", nullable: true),
                    perfil_acesso = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    login_token = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    login_data_cadastro = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login", x => x.login_id);
                    table.ForeignKey(
                        name: "FK_login_empresas_emp_id",
                        column: x => x.emp_id,
                        principalTable: "empresas",
                        principalColumn: "emp_id");
                    table.ForeignKey(
                        name: "FK_login_funcionarios_func_id",
                        column: x => x.func_id,
                        principalTable: "funcionarios",
                        principalColumn: "func_id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "eventos",
                columns: table => new
                {
                    even_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    even_nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    even_tipo = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    even_status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    even_data = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    emp_id = table.Column<int>(type: "int", nullable: false),
                    clien_id = table.Column<int>(type: "int", nullable: false),
                    even_valor_total_orcamento = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    even_local_tipo = table.Column<int>(type: "int", nullable: false),
                    even_nome_local_proprio = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    even_valor_local_proprio = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    even_produto_local_id = table.Column<int>(type: "int", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    data_termino = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos", x => x.even_id);
                    table.ForeignKey(
                        name: "FK_eventos_clientes_clien_id",
                        column: x => x.clien_id,
                        principalTable: "clientes",
                        principalColumn: "clien_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eventos_produtos_fornecedor_even_produto_local_id",
                        column: x => x.even_produto_local_id,
                        principalTable: "produtos_fornecedor",
                        principalColumn: "prod_id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "evento_itens",
                columns: table => new
                {
                    evit_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    even_id = table.Column<int>(type: "int", nullable: false),
                    prod_id = table.Column<int>(type: "int", nullable: true),
                    evit_nome_exibicao = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    evit_quantidade = table.Column<int>(type: "int", nullable: false),
                    evit_valor_custo = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    evit_valor_venda = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    evit_status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evento_itens", x => x.evit_id);
                    table.ForeignKey(
                        name: "FK_evento_itens_eventos_even_id",
                        column: x => x.even_id,
                        principalTable: "eventos",
                        principalColumn: "even_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_categorias_servico_emp_id",
                table: "categorias_servico",
                column: "emp_id");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_emp_id",
                table: "clientes",
                column: "emp_id");

            migrationBuilder.CreateIndex(
                name: "IX_evento_itens_even_id",
                table: "evento_itens",
                column: "even_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_clien_id",
                table: "eventos",
                column: "clien_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_even_produto_local_id",
                table: "eventos",
                column: "even_produto_local_id");

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_emp_id",
                table: "fornecedores",
                column: "emp_id");

            migrationBuilder.CreateIndex(
                name: "IX_funcionarios_emp_id",
                table: "funcionarios",
                column: "emp_id");

            migrationBuilder.CreateIndex(
                name: "IX_login_emp_id",
                table: "login",
                column: "emp_id");

            migrationBuilder.CreateIndex(
                name: "IX_login_func_id",
                table: "login",
                column: "func_id");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_fornecedor_cat_id",
                table: "produtos_fornecedor",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_fornecedor_forn_id",
                table: "produtos_fornecedor",
                column: "forn_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checklist");

            migrationBuilder.DropTable(
                name: "convidados");

            migrationBuilder.DropTable(
                name: "evento_itens");

            migrationBuilder.DropTable(
                name: "login");

            migrationBuilder.DropTable(
                name: "logs");

            migrationBuilder.DropTable(
                name: "eventos");

            migrationBuilder.DropTable(
                name: "funcionarios");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "produtos_fornecedor");

            migrationBuilder.DropTable(
                name: "categorias_servico");

            migrationBuilder.DropTable(
                name: "fornecedores");

            migrationBuilder.DropTable(
                name: "empresas");
        }
    }
}
