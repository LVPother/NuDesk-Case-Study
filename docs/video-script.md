# Demo video script — Weekly Sales Coach (~2:30)

Before recording: app running in demo mode or with the Claude key (live is nicer, ~30 s per draft with Haiku; if you use live, pre-generate Carlos before recording and skip the wait). Browser at 1440 px wide, zoom 100 %, signed out. Close other tabs.

---

## 0:00 – 0:20 · The problem (login page on screen)

> Hi, I'm Eduardo. This is Weekly Sales Coach, a small tool I built for the nuDesk exercise.
>
> The problem it solves: sales reps almost never get feedback they can act on. It comes late, it's vague, and managers don't have hours every week to write it for each person. So it doesn't happen.
>
> The idea: let the AI write the first draft from the data, and let the manager decide.

## 0:20 – 0:45 · Manager dashboard

*Clic en "Sign in as Roberto Díaz".*

> I'm signed in as Roberto, the sales manager. The demo team are loan officers, because nuDesk works in financial services, but the vocabulary is configuration, not code.
>
> Team numbers at the top, one card per rep, and the status of each one's coaching. Laura's is already approved. Let's do Carlos.

## 0:45 – 1:20 · Review: evidence and generation

*Clic en la tarjeta de Carlos.*

> On the left is exactly what the AI sees, nothing more: his numbers versus last week and the team average, and every deal in his pipeline. Notice four applications stuck in Documents Pending for a week or more.
>
> I can add context the AI wouldn't know, like "out two days for training". Then Generate.

*Clic en "Generate draft with AI". Si es en vivo, corta la espera en edición.*

> Every point cites evidence: Summit Auto Repair, ten days pending, no contact in nine. Not "improve your follow-up", but the specific file and the specific number.

## 1:20 – 1:50 · Edit and approve

*Ya en el editor a pantalla completa. Cambia una palabra en el resumen, "Save edits", luego "Approve & share with Carlos".*

> The draft opens on its own screen. I can change anything. If I try to leave with unsaved edits, it warns me.
>
> Nothing reaches Carlos until I press this button. When I do, the text is frozen, and an email is composed and queued for delivery.

*Muestra el aviso verde y "Preview email" en la página de revisión.*

## 1:50 – 2:15 · The rep's view

*Sign out, "Sign in as Carlos Mendoza".*

> Carlos signs in and sees "your feedback is ready", his own numbers with small charts, his pipeline, and the coaching in reading mode. Past weeks stay here.

*Clic en "Read my feedback", scroll lento por el texto.*

## 2:15 – 2:35 · The rules and the close

*Vuelve al dashboard o quédate en la vista del rep.*

> Three rules shaped everything. Coaching, not scoring: no rankings, no grades. Human in the loop: the manager approves every word. Privacy by design: the AI only sees the rep being coached, never the others.
>
> It's .NET 10 and Blazor with SQLite, Claude through the official SDK with a fixed output schema, ninety-one tests, and a demo mode that runs without any API key. The README explains the business side; README-DEV has the architecture. Thanks for watching.

---

### Si te sobran 20 segundos (opcional)

*En el reporte aprobado de Laura, clic en "Revise feedback".*

> And if the manager changes their mind, "Revise feedback" reopens an approved report, the rep stops seeing it, and a second, updated email goes out on re-approval.

### Consejos de grabación

- Graba en dos o tres tomas y corta las esperas de la IA; nadie quiere ver 30 segundos de spinner.
- Lee el guion como conversación, no de corrido. Si te equivocas, pausa dos segundos y repite la frase; se corta fácil.
- Herramientas gratis: OBS, o Xbox Game Bar (Win+G) para capturar la ventana del navegador.
- Sube el video a YouTube como "no listado" o a Loom y pon el enlace al inicio del README.
