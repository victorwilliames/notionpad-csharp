# 📝 NotionPad

> **O Editor de Texto Minimalista e Ultrarrápido que une o melhor do Bloco de Notas do Windows, a sofisticação visual do Notion e o poder de código do Notepad++.**

[![Platform](https://img.shields.io/badge/Plataforma-Windows%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Tech](https://img.shields.io/badge/Tecnologia-C%23%20%7C%20WPF%20%7C%20.NET%204.8-512BD4?logo=csharp&logoColor=white)](https://dotnet.microsoft.com)
[![Binary Size](https://img.shields.io/badge/Tamanho-134%20KB%20(Portátil)-2ea44f)](#)
[![Zero Dependencies](https://img.shields.io/badge/Dependências-Zero%20(Nativo)-blue)](#)
[![License](https://img.shields.io/badge/Licença-MIT-yellow.svg)](LICENSE)

---

## 📌 Visão Geral e Filosofia

O **NotionPad** nasceu com uma premissa clara: **a maioria dos editores de texto modernos ficou lenta, pesada e poluída**. Aplicativos baseados em Electron (como VS Code, Notion oficial ou Obsidian) consomem entre **200 MB a 600 MB de memória RAM** só para abrir um arquivo de texto e demoram segundos para inicializar. Por outro lado, o Bloco de Notas clássico é excessivamente simples e não oferece suporte a realce de código, organização em abas modernas ou estética agradável.

O NotionPad resolve esse dilema unindo três mundos:
1. **A velocidade instantânea e o consumo ínfimo do Bloco de Notas nativo** (~30 MB de RAM, inicialização em ~100 ms).
2. **A experiência visual e de personalização do Notion** (capas em degradê/galeria, reposicionamento de imagem, ícones de emojis e barra de ferramentas flutuante).
3. **A produtividade técnica do Notepad++** (reconhecimento automático de linguagens, realce de sintaxe colorido, busca e substituição sem pop-ups intrusivos).

> 🥇 **Regra de Ouro do Projeto**: **Performance Absoluta e Usabilidade.**
> Zero atraso de digitação (input lag = 0 ms a 60+ FPS nativo), sem congelamentos na interface, arquivo binário único sem instalador e total preservação dos arquivos em disco.

---

## 💻 Stack Tecnológica (Como foi feito)

O NotionPad foi construído com arquitetura de baixo nível e alta performance para Windows, sem qualquer dependência externa ou pacotes pesados:

| Componente | Tecnologia Utilizada | Motivo da Escolha |
| :--- | :--- | :--- |
| **Linguagem** | **C#** (especificação C# 5.0) | Permite compilação direta pelo compilador nativo do Windows sem ferramentas adicionais. |
| **Interface (GUI)** | **WPF (Windows Presentation Foundation)** | Renderização acelerada por GPU (DirectX/Milcore), animações fluidas a 60 FPS e suporte a alta densidade de pixels (HiDPI/4K). |
| **Framework Base** | **.NET Framework 4.8 / 4.0** | Já vem pré-instalado em todas as versões do **Windows 10** e **Windows 11**. Não exige baixar runtimes. |
| **Compilador** | **`csc.exe` Nativo do Windows** | O projeto não precisa de Visual Studio, Node.js, npm, Python ou Gradle para ser compilado. Um simples script em lote (`build.bat`) gera o executável em **1 segundo**. |
| **Distribuição** | **Executável Único (`NotionPad.exe`)** | Apenas **~134 KB**. 100% portátil: copie para um pendrive ou envie para qualquer computador e execute com 1 clique. |
| **Sem Electron / Sem Web** | **C# Puro** | Elimina a sobrecarga de carregar um navegador Chromium em segundo plano. |

---

## 🚀 Arquitetura e Engenharia de Software

```
┌────────────────────────────────────────────────────────────────────────┐
│                              NotionPad.exe                             │
├────────────────────────────────────────────────────────────────────────┤
│  [TopBar] Abas Dinâmicas | Botão Toggle de Menus | Ações de Janela    │
├────────────────────────────────────────────────────────────────────────┤
│  [MenuBar (Oculta por padrão)] Arquivo | Editar | Exibir | Config ⚙   │
├────────────────────────────────────────────────────────────────────────┤
│  [FindBar In-Place] Localizar & Substituir com Contador ao Vivo       │
├────────────────────────────────────────────────────────────────────────┤
│  [Editor Canvas - ScrollViewer com Suporte a HiDPI]                   │
│   ├── Notion Header:                                                   │
│   │    ├── Cover Banner (Degradês / Galeria / Upload / Reposicionamento)│
│   │    ├── Emoji Badge (Seletor com busca inteligente)                 │
│   │    └── Título da Página com placeholder dinâmico                  │
│   └── RichTextBox de Alta Performance:                                │
│        ├── Motor de Sintaxe com Debounce Inteligente (400ms)           │
│        └── Floating Toolbar Flutuante ao selecionar texto             │
├────────────────────────────────────────────────────────────────────────┤
│  [StatusBar] Ln X, Col Y | Caracteres | [Linguagem ▾] | Zoom | UTF-8   │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
        Persistência Invisível: %APPDATA%\NotionPad\page_metadata.tsv
        (Arquivos .txt/.html/.md/.js no disco permanecem 100% limpos)
```

---

## ✨ Funcionalidades em Detalhes

### 1. Interface Ultra-Clean e Menu Toggle
- **Barra de Menus Oculta por Padrão:** ao abrir o aplicativo, o usuário é recebido por uma tela limpa, focada 100% no conteúdo.
- **Alternância com 1 Clique (Toggle):** clicar no ícone do NotionPad no canto superior esquerdo ou pressionar `Ctrl+M` / `F10` exibe ou recolhe a barra de menus instantaneamente.
- **Redimensionamento Inteligente:** quando a barra é recolhida, o editor ocupa todo o espaço vertical imediatamente, sem faixas vazias.

### 2. Reconhecimento Automático de Linguagens (Estilo Notepad++)
O editor detecta o formato do arquivo automaticamente ao abrir ou salvar, ativando as regras corretas de coloração:
- **Web & Dados:** `.html`, `.htm`, `.css`, `.js`, `.json`, `.xml`, `.svg`
- **Programação & Scripts:** `.py`, `.cs`, `.sql`
- **Documentação & Notas:** `.md`, `.txt`, `.log`, `.ini`, `.env`
- **Seletor de Linguagem Interativo:** na barra de status inferior direita, há um botão clicável (ex: `[ HTML ▾ ]`). Clicar nele permite trocar manualmente a sintaxe de qualquer documento para visualização imediata.

### 3. Motor de Realce de Sintaxe com Zero Input Lag
Para honrar a **Regra de Ouro de Performance**:
- **Cores calibradas no Dark Mode do Notion:**
  - 🔵 **Tags e Palavras-chave:** `#569CD6`
  - 🔷 **Atributos, Funções e Propriedades:** `#9CDCFE`
  - 🟠 **Strings e Textos:** `#CE9178`
  - 🟢 **Comentários:** `#6A9955`
  - 🍏 **Números e Booleanos:** `#B5CEA8`
  - 💠 **Tipos, Classes e Títulos Markdown:** `#4EC9B0`
  - 🟡 **Entidades e Doctype:** `#D7BA7D`
- **Debounce de 400ms:** durante a digitação contínua, o editor não executa re-tokenização pesada, operando na velocidade máxima nativa do WPF (60+ FPS, 0 ms de atraso). Ao fazer uma pausa, apenas a linha/parágrafo do cursor é reformatada em **~1 milissegundo**, mantendo o cursor exatamente onde estava.
- **Modo Texto Puro:** em arquivos `.txt`, o analisador fica totalmente inativo para entregar a velocidade bruta de um bloco de notas tradicional.

### 4. Busca & Substituição Fluida In-Place
- **Sem pop-ups modais:** o antigo `MessageBox.Show` foi completamente substituído por uma barra de busca moderna no topo.
- **Contador em tempo real:** exibe o número de ocorrências na hora (ex: `1 de 14`, `3 de 15` ou `Nenhum resultado` em vermelho suave).
- **Seleção ativa mantida:** o texto localizado é selecionado e rolado suavemente para a visão do usuário enquanto o foco permanece na digitação da caixa de pesquisa.
- **Substituição Atômica:** o botão "Substituir" troca e avança; o botão "Substituir tudo" realiza as substituições de trás para frente (garantindo que os ponteiros e índices nunca se percam) e reporta o total (ex: `14 substituídos`).

### 5. Sistema de Capas Fiel ao Notion (Cover Experience)
- **Cabeçalho Integrado:** adicione capas à página com proporções e posicionamento idênticos aos do Notion.
- **Seletor Completo com Abas:**
  - **Galeria:** capas predefinidas em degradês modernos e elegantes.
  - **Cores Sólidas:** opções discretas e minimalistas.
  - **Upload:** permite escolher imagens locais do computador (`.png`, `.jpg`, `.jpeg`, `.webp`).
  - **Link:** permite colar a URL direta de qualquer imagem da internet.
  - **Remover:** exclui a capa e retorna a página ao estado limpo.
- **Reposicionamento Interativo:** ao clicar em "Reposicionar", basta arrastar a imagem verticalmente com o mouse para ajustar o enquadramento perfeito, com ações de "Salvar posição" e "Cancelar".
- **Hover Discreto:** os botões de ação da capa ficam totalmente invisíveis e só aparecem suavemente ao passar o mouse sobre o cabeçalho.

### 6. Ícones de Emojis e Barra Flutuante
- **Ícone da Página:** adicione emojis à página que se sincronizam com o título da aba.
- **Busca Rápida de Emojis:** seletor com pesquisa inteligente em português.
- **Floating Toolbar:** ao selecionar qualquer palavra ou frase no texto, surge uma barra flutuante com sombra suave para formatação rápida (**Negrito**, *Itálico*, <u>Sublinhado</u>, ~~Tachado~~, **Marca-texto Notion**, H1, H2, H3 e marcadores).

### 7. Armazenamento Invisível de Metadados (Clean Files)
- Se você salvar um arquivo `.txt`, `.md`, `.html` ou `.js`, ele será salvo **100% puro no disco**, sem marcas d'água, sem tags proprietárias e sem comentários intrusivos.
- Os metadados visuais (cor da capa, imagem, posição vertical e emoji) são guardados de forma invisível no banco local do usuário (`%APPDATA%\NotionPad\page_metadata.tsv`). Ao abrir o mesmo arquivo novamente no NotionPad, toda a identidade visual é restaurada com precisão.

---

## ⌨️ Atalhos de Teclado

| Atalho | Ação |
| :--- | :--- |
| `Ctrl + M` ou `F10` | Alternar exibição da barra de menus |
| `Ctrl + T` | Criar nova guia (página em branco) |
| `Ctrl + W` | Fechar guia atual |
| `Ctrl + Tab` | Alternar entre guias abertas |
| `Ctrl + N` | Abrir nova janela |
| `Ctrl + O` | Abrir arquivo do computador |
| `Ctrl + S` | Salvar arquivo |
| `Ctrl + Shift + S` | Salvar como... |
| `Ctrl + F` | Abrir barra de localização |
| `Ctrl + H` | Abrir barra de substituição |
| `Enter` (na busca) / `F3` | Ir para a próxima ocorrência |
| `Shift + Enter` / `Shift + F3` | Ir para a ocorrência anterior |
| `Esc` | Fechar busca, popups ou cancelar reposicionamento |
| `Ctrl + B` | Negrito no texto selecionado |
| `Ctrl + I` | Itálico no texto selecionado |
| `Ctrl + U` | Sublinhado no texto selecionado |
| `Ctrl + +` / `Ctrl + -` | Aumentar / Diminuir zoom |
| `Ctrl + 0` | Restaurar zoom padrão (100%) |
| `F5` | Inserir Hora e Data atual |

---

## 🛠️ Como Compilar

O projeto conta com um script de compilação automatizado de 1 clique:

### Opção 1: Via Duplo-Clique
Dê um duplo clique no arquivo `build.bat` na raiz da pasta.

### Opção 2: Via Terminal (CMD / PowerShell)
```cmd
cd "C:\.PROJETOS - Sites 2026\clonagem\notionpad"
build.bat
```

### O que o script faz por baixo dos panos:
Ele invoca diretamente o compilador nativo C# do Windows de 64 bits:
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /t:winexe /optimize+ /lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF" /win32icon:"src\notionpad.ico" /out:"NotionPad.exe" /r:PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.Xaml.dll,System.dll,System.Core.dll "src\NotionPad.cs"
```
Tempo de compilação médio: **~1 segundo**.

---

## 📁 Estrutura de Arquivos do Projeto

A pasta do projeto foi desenhada para ser limpa e conter rigorosamente apenas o que é essencial:

```text
notionpad/
├── src/
│   ├── NotionPad.cs          # Código-fonte completo em C# (GUI WPF, Motores de Sintaxe e Busca)
│   └── notionpad.ico         # Ícone oficial em alta resolução
├── .gitignore                # Regras limpas para versionamento Git
├── build.bat                 # Script de compilação em lote (1 clique)
├── LICENSE                   # Licença MIT
├── NotionPad.exe             # Binário compilado portátil (~134 KB)
└── README.md                 # Documentação completa do projeto
```

---

## 📄 Licença

Distribuído sob a licença **MIT**. Você tem liberdade para utilizar, modificar e distribuir o projeto como desejar.
