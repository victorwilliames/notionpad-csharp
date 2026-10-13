# 📝 NotionPad

> **O Editor de Texto Minimalista que une o melhor do Bloco de Notas Moderno com a Experiência do Notion.**

O **NotionPad** é um editor de texto de alto desempenho, portátil e de código aberto para Windows, construído para proporcionar uma experiência de escrita limpa, fluida e focada. Ele combina a estética moderna do Windows 11 Fluent Design com os melhores recursos de edição e personalização do Notion.

---

## ✨ Principais Funcionalidades

### 🎨 1. Interface Ultra-Clean e Minimalista
- **Barra de Menus Oculta por Padrão**: Foco total na escrita sem poluição visual.
- **Botão de Alternância (Toggle)**: Clique no ícone azul no canto superior esquerdo ou no ícone da aba para exibir ou ocultar os menus (`Arquivo`, `Editar`, `Exibir` e `Configurações ⚙`).
- **Aproveitamento Total da Tela**: Quando a barra está recolhida, a área de edição expande automaticamente, sem espaços vazios.

### 🖼️ 2. Sistema Completo de Capas (Estilo Notion)
- **Cabeçalho com Capa Integrada**: Adicione capas elegantes às suas notas.
- **Galeria Exclusiva**: Cores sólidas, degradês modernos e texturas visuais.
- **Upload e Link**: Insira imagens locais do computador ou URLs da web.
- **Reposicionamento Interativo**: Ajuste o enquadramento vertical da capa simplesmente arrastando a imagem com o mouse.
- **Menu Contextual de Hover**: Ações de *Alterar capa* e *Reposicionar* só aparecem ao passar o mouse.

### 🙂 3. Ícones e Seletor de Emojis
- Adicione ícones personalizados às suas páginas.
- Seletor de emojis integrado com busca rápida por palavras-chave.
- Sincronização automática com o título da aba.

### ⚡ 4. Barra de Ferramentas Flutuante
- Selecione qualquer trecho de texto para que a barra de ferramentas apareça suavemente sobre a seleção.
- Aplique **Negrito**, *Itálico*, <u>Sublinhado</u>, ~~Tachado~~, **Marca-texto (Destaque Notion)** e links.
- Tipos de bloco rápidos: Título 1, Título 2, Título 3, Listas com marcadores/numeradas e Citação.

### 📑 5. Navegação por Abas (Tabs)
- Trabalhe em múltiplos documentos simultaneamente com guias dinâmicas.
- Suporte a arrastar e soltar arquivos (Drag & Drop) de qualquer lugar para abrir.
- Atalhos rápidos para abrir (`Ctrl+T`), fechar (`Ctrl+W`) e navegar entre abas (`Ctrl+Tab`).

### 📦 6. Portátil e Sem Instalação
- Executável autônomo de arquivo único (`NotionPad.exe`).
- Compatível nativamente com **Windows 10** e **Windows 11** sem necessidade de instalar Node.js, Python, Electron ou frameworks pesados.

---

## ⌨️ Atalhos de Teclado

| Atalho | Ação |
| :--- | :--- |
| `Ctrl + M` ou `F10` | Alternar exibição da barra de menus |
| `Ctrl + T` | Criar nova guia (página limpa) |
| `Ctrl + W` | Fechar guia atual |
| `Ctrl + Tab` | Alternar entre guias abertas |
| `Ctrl + N` | Abrir nova janela |
| `Ctrl + O` | Abrir arquivo do computador |
| `Ctrl + S` | Salvar arquivo atual |
| `Ctrl + Shift + S` | Salvar como... |
| `Ctrl + B` | Negrito no texto selecionado |
| `Ctrl + I` | Itálico no texto selecionado |
| `Ctrl + U` | Sublinhado no texto selecionado |
| `Ctrl + F` | Localizar texto |
| `Ctrl + H` | Substituir texto |
| `Ctrl + +` / `Ctrl + -` | Aumentar / diminuir zoom |
| `Ctrl + 0` | Restaurar zoom padrão (100%) |
| `F5` | Inserir Hora e Data |
| `Esc` | Fechar popups, barra de busca ou cancelar reposicionamento |

---

## 🛠️ Como Compilar

O código-fonte é 100% C# nativo (.NET Framework / WPF), compilável diretamente pelo compilador do Windows (`csc.exe`):

1. Dê um duplo-clique no arquivo `build.bat` ou execute no terminal:
   ```cmd
   build.bat
   ```
2. O binário `NotionPad.exe` será gerado imediatamente na raiz do diretório.

---

## 📄 Licença

Distribuído sob a licença **MIT**. Veja o arquivo `LICENSE` para mais detalhes.
