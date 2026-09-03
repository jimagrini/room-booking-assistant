import { useEffect, useRef, useState } from 'react'
import type {
  FormEvent,
  KeyboardEvent,
  ReactNode,
} from 'react'
import {
  ApiError,
  login,
  sendAssistantMessage,
} from './api'
import type { AssistantMessageResponse, LoginResponse } from './api'
import './App.css'

const sessionStorageKey = 'room-booking.session'

const toolLabels: Record<string, string> = {
  list_available_rooms: 'Consultó salas disponibles',
  get_room_schedule: 'Consultó la agenda de una sala',
  list_my_bookings: 'Consultó tus reservas',
  create_booking: 'Creó una reserva',
  cancel_booking: 'Canceló una reserva',
}

const suggestions = [
  '¿Qué salas están disponibles mañana de 13:00 a 14:00 para 5 personas?',
  'Mostrame mis reservas activas.',
  '¿Cuál es la agenda de la sala B para mañana?',
]

type MessageRole = 'assistant' | 'user' | 'error'

interface ChatMessage {
  id: string
  role: MessageRole
  content: string
  toolsUsed?: string[]
}

function createInitialMessages(): ChatMessage[] {
  return [
    {
      id: crypto.randomUUID(),
      role: 'assistant',
      content:
        '¡Hola! Puedo buscar salas disponibles, consultar agendas, crear reservas y administrar tus reuniones. ¿Qué necesitás?',
    },
  ]
}

function readStoredSession(): LoginResponse | null {
  try {
    const stored = sessionStorage.getItem(sessionStorageKey)
    if (!stored) {
      return null
    }

    const session = JSON.parse(stored) as LoginResponse
    if (
      !session.accessToken ||
      !session.user?.username ||
      new Date(session.expiresAtUtc).getTime() <= Date.now()
    ) {
      sessionStorage.removeItem(sessionStorageKey)
      return null
    }

    return session
  } catch {
    sessionStorage.removeItem(sessionStorageKey)
    return null
  }
}

function inlineMarkdown(text: string): ReactNode[] {
  return text
    .split(/(\*\*.*?\*\*|\*[^*]+\*|`.*?`)/g)
    .filter(Boolean)
    .map((part, index) => {
      if (part.startsWith('**') && part.endsWith('**')) {
        return <strong key={`${part}-${index}`}>{part.slice(2, -2)}</strong>
      }

      if (part.startsWith('`') && part.endsWith('`')) {
        return <code key={`${part}-${index}`}>{part.slice(1, -1)}</code>
      }

      if (part.startsWith('*') && part.endsWith('*')) {
        return <em key={`${part}-${index}`}>{part.slice(1, -1)}</em>
      }

      return part
    })
}

function parseTableRow(line: string): string[] {
  return line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => cell.trim())
}

function isTableSeparator(line: string): boolean {
  const cells = parseTableRow(line)
  return cells.length > 0 && cells.every((cell) => /^:?-{3,}:?$/.test(cell))
}

function MessageContent({ content }: { content: string }) {
  const lines = content.split(/\r?\n/)
  const blocks: ReactNode[] = []

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index].trim()

    if (!line) {
      continue
    }

    if (
      line.startsWith('|') &&
      index + 1 < lines.length &&
      isTableSeparator(lines[index + 1])
    ) {
      const headers = parseTableRow(line)
      const rows: string[][] = []
      index += 2

      while (index < lines.length && lines[index].trim().startsWith('|')) {
        rows.push(parseTableRow(lines[index]))
        index += 1
      }

      index -= 1
      blocks.push(
        <div className="message-table-wrap" key={`table-${index}`}>
          <table>
            <thead>
              <tr>
                {headers.map((header, cellIndex) => (
                  <th key={`${header}-${cellIndex}`}>{inlineMarkdown(header)}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, rowIndex) => (
                <tr key={`row-${rowIndex}`}>
                  {row.map((cell, cellIndex) => (
                    <td key={`${cell}-${cellIndex}`}>{inlineMarkdown(cell)}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>,
      )
      continue
    }

    if (/^[-*]\s+/.test(line)) {
      const items: string[] = []

      while (
        index < lines.length &&
        /^[-*]\s+/.test(lines[index].trim())
      ) {
        items.push(lines[index].trim().replace(/^[-*]\s+/, ''))
        index += 1
      }

      index -= 1
      blocks.push(
        <ul key={`list-${index}`}>
          {items.map((item, itemIndex) => (
            <li key={`${item}-${itemIndex}`}>{inlineMarkdown(item)}</li>
          ))}
        </ul>,
      )
      continue
    }

    const heading = line.match(/^#{1,3}\s+(.+)$/)
    if (heading) {
      blocks.push(
        <h3 key={`heading-${index}`}>{inlineMarkdown(heading[1])}</h3>,
      )
      continue
    }

    blocks.push(<p key={`paragraph-${index}`}>{inlineMarkdown(line)}</p>)
  }

  return <>{blocks}</>
}

function friendlyError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) {
      return 'Usuario o contraseña incorrectos.'
    }

    return error.message
  }

  return 'Ocurrió un error inesperado. Intentá nuevamente.'
}

function App() {
  const [session, setSession] = useState<LoginResponse | null>(readStoredSession)
  const [username, setUsername] = useState('User1')
  const [password, setPassword] = useState('')
  const [authError, setAuthError] = useState('')
  const [isLoggingIn, setIsLoggingIn] = useState(false)
  const [messages, setMessages] = useState<ChatMessage[]>(createInitialMessages)
  const [conversationId, setConversationId] = useState<string>()
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const messagesEndRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isSending])

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setAuthError('')
    setIsLoggingIn(true)

    try {
      const nextSession = await login(username.trim(), password)
      sessionStorage.setItem(sessionStorageKey, JSON.stringify(nextSession))
      setSession(nextSession)
      setPassword('')
    } catch (error) {
      setAuthError(friendlyError(error))
    } finally {
      setIsLoggingIn(false)
    }
  }

  function handleLogout() {
    sessionStorage.removeItem(sessionStorageKey)
    setSession(null)
    setConversationId(undefined)
    setMessages(createInitialMessages())
    setDraft('')
  }

  function handleNewConversation() {
    setConversationId(undefined)
    setMessages(createInitialMessages())
    setDraft('')
  }

  async function submitMessage(rawMessage: string) {
    const message = rawMessage.trim()
    if (!message || !session || isSending) {
      return
    }

    setMessages((current) => [
      ...current,
      {
        id: crypto.randomUUID(),
        role: 'user',
        content: message,
      },
    ])
    setDraft('')
    setIsSending(true)

    try {
      let response: AssistantMessageResponse

      try {
        response = await sendAssistantMessage(
          session.accessToken,
          message,
          conversationId,
        )
      } catch (error) {
        if (
          error instanceof ApiError &&
          error.code === 'assistant.conversation_not_found' &&
          conversationId
        ) {
          response = await sendAssistantMessage(session.accessToken, message)
        } else {
          throw error
        }
      }

      setConversationId(response.conversationId)
      setMessages((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          role: 'assistant',
          content: response.message,
          toolsUsed: response.toolsUsed,
        },
      ])
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        sessionStorage.removeItem(sessionStorageKey)
        setSession(null)
        setConversationId(undefined)
        setMessages(createInitialMessages())
        setAuthError('Tu sesión venció. Ingresá nuevamente.')
        return
      }

      setMessages((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          role: 'error',
          content: friendlyError(error),
        },
      ])
    } finally {
      setIsSending(false)
    }
  }

  function handleSend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void submitMessage(draft)
  }

  function handleComposerKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      event.currentTarget.form?.requestSubmit()
    }
  }

  if (!session) {
    return (
      <main className="login-page">
        <section className="login-intro">
          <div className="brand brand-light">
            <span className="brand-mark" aria-hidden="true">R</span>
            <span>Room Booking</span>
          </div>

          <div className="intro-copy">
            <span className="eyebrow">Asistente inteligente</span>
            <h1>Reservá la sala correcta conversando.</h1>
            <p>
              Consultá disponibilidad, creá reuniones y administrá tus reservas
              desde un único lugar.
            </p>
          </div>

          <div className="intro-preview" aria-hidden="true">
            <div className="preview-dot" />
            <span>“Necesito una sala mañana a las 14:00 para 6 personas”</span>
          </div>
        </section>

        <section className="login-panel">
          <form className="login-card" onSubmit={handleLogin}>
            <div className="mobile-brand brand">
              <span className="brand-mark" aria-hidden="true">R</span>
              <span>Room Booking</span>
            </div>
            <div className="login-heading">
              <span className="eyebrow">Bienvenido</span>
              <h2>Iniciá sesión</h2>
              <p>Usá una de las cuentas habilitadas para el challenge.</p>
            </div>

            <label>
              Usuario
              <input
                autoComplete="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                placeholder="User1"
                required
              />
            </label>

            <label>
              Contraseña
              <input
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder="Ingresá tu contraseña"
                required
              />
            </label>

            {authError && <div className="form-error" role="alert">{authError}</div>}

            <button className="primary-button" type="submit" disabled={isLoggingIn}>
              {isLoggingIn ? 'Ingresando…' : 'Ingresar'}
            </button>

            <p className="login-hint">
              Cuentas disponibles: <strong>User1</strong> y <strong>User2</strong>
            </p>
          </form>
        </section>
      </main>
    )
  }

  return (
    <main className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>Room Booking</span>
        </div>

        <button className="new-chat-button" type="button" onClick={handleNewConversation}>
          <span aria-hidden="true">＋</span>
          Nueva conversación
        </button>

        <div className="sidebar-section">
          <span className="sidebar-label">Ideas para comenzar</span>
          {suggestions.map((suggestion) => (
            <button
              className="suggestion-button"
              type="button"
              key={suggestion}
              disabled={isSending}
              onClick={() => void submitMessage(suggestion)}
            >
              {suggestion}
            </button>
          ))}
        </div>

        <div className="sidebar-user">
          <div className="avatar">{session.user.username.charAt(0).toUpperCase()}</div>
          <div>
            <strong>{session.user.username}</strong>
            <span>Sesión activa</span>
          </div>
          <button type="button" onClick={handleLogout} aria-label="Cerrar sesión">
            Salir
          </button>
        </div>
      </aside>

      <section className="chat-workspace">
        <header className="chat-header">
          <div>
            <span className="eyebrow">Asistente de reservas</span>
            <h1>¿En qué puedo ayudarte?</h1>
          </div>
          <div className="service-status">
            <span aria-hidden="true" />
            API conectada
          </div>
        </header>

        <div className="messages" aria-live="polite">
          <div className="messages-inner">
            {messages.map((message) => (
              <article className={`message message-${message.role}`} key={message.id}>
                <div className="message-avatar" aria-hidden="true">
                  {message.role === 'user'
                    ? session.user.username.charAt(0).toUpperCase()
                    : message.role === 'error'
                      ? '!'
                      : 'R'}
                </div>
                <div className="message-body">
                  <div className="message-meta">
                    {message.role === 'user'
                      ? session.user.username
                      : message.role === 'error'
                        ? 'No se pudo completar'
                        : 'Asistente'}
                  </div>
                  <div className="message-content">
                    <MessageContent content={message.content} />
                  </div>
                  {message.toolsUsed && message.toolsUsed.length > 0 && (
                    <div className="tool-list">
                      {message.toolsUsed.map((tool) => (
                        <span key={tool}>{toolLabels[tool] ?? tool}</span>
                      ))}
                    </div>
                  )}
                </div>
              </article>
            ))}

            {isSending && (
              <article className="message message-assistant">
                <div className="message-avatar" aria-hidden="true">R</div>
                <div className="message-body">
                  <div className="message-meta">Asistente</div>
                  <div className="typing-indicator" aria-label="El asistente está escribiendo">
                    <span />
                    <span />
                    <span />
                  </div>
                </div>
              </article>
            )}
            <div ref={messagesEndRef} />
          </div>
        </div>

        <footer className="composer-area">
          <form className="composer" onSubmit={handleSend}>
            <textarea
              aria-label="Mensaje para el asistente"
              placeholder="Escribí tu solicitud de reserva…"
              rows={1}
              value={draft}
              disabled={isSending}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={handleComposerKeyDown}
            />
            <button
              type="submit"
              aria-label="Enviar mensaje"
              disabled={isSending || !draft.trim()}
            >
              <span aria-hidden="true">↑</span>
            </button>
          </form>
          <div className="composer-meta">
            <span>Enter para enviar · Shift + Enter para nueva línea</span>
            {conversationId && <span>Conversación {conversationId.slice(0, 8)}</span>}
          </div>
        </footer>
      </section>
    </main>
  )
}

export default App
