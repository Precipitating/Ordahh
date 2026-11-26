<script setup>
import { ref, computed, onMounted } from 'vue'
import { useApi } from '../composables/useApi'
import { readError } from '../api'
import StaffNav from '../components/StaffNav.vue'

const { authFetch } = useApi()

const items = ref([])
const loading = ref(true)
const error = ref('')
const actionError = ref('')
const busyId = ref(null)
const showArchived = ref(false)

// form state: editingId === null means "adding"
const formOpen = ref(false)
const editingId = ref(null)
const form = ref(emptyForm())
const formError = ref('')
const saving = ref(false)

function emptyForm() {
      return { name: '', description: '', price: null, categoryId: null }
}

// hide archived rows unless asked; keep them grouped by category, then name
const visibleItems = computed(() => {
      const list = showArchived.value
        ? items.value
        : items.value.filter(i => !i.isArchived)

      return [...list].sort((a, b) =>
        a.categoryName.localeCompare(b.categoryName) || a.name.localeCompare(b.name)
      )
})

// the dropdown options come from the categories the items already carry
const categories = computed(() => {
      const seen = new Map()
      for (const i of items.value) seen.set(i.categoryId, i.categoryName)
      return [...seen].map(([id, name]) => ({ id, name }))
})

async function load() {
      try {
        const response = await authFetch('/api/admin/menu')
        if (!response.ok) throw new Error(`Server returned ${response.status}`)
        items.value = await response.json()
        error.value = ''
      } catch (e) {
        if (e.message !== 'Session expired') error.value = 'Could not load the menu.'
        console.error(e)
      } finally {
        loading.value = false
      }
}

onMounted(load)

function replaceItem(updated) {
      const index = items.value.findIndex(i => i.id === updated.id)
      if (index !== -1) items.value[index] = updated
}

// ---- quick toggles ----

async function patch(item, part, body) {
      if (busyId.value !== null) return

      busyId.value = item.id
      actionError.value = ''

      try {
        const response = await authFetch(`/api/admin/menu-items/${item.id}/${part}`, {
          method: 'PATCH',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        })

        if (!response.ok) {
          actionError.value = await readError(response, 'Could not update the item.')
          return
        }
        replaceItem(await response.json())
      } catch (e) {
        if (e.message !== 'Session expired') actionError.value = 'Could not reach the server.'
        console.error(e)
      } finally {
        busyId.value = null
      }
}

function toggleAvailability(item) {
      return patch(item, 'availability', { isAvailable: !item.isAvailable })
}

function toggleArchive(item) {
      if (!item.isArchived && !confirm(`Archive "${item.name}"? Customers will no longer see it.`)) return
      return patch(item, 'archive', { isArchived: !item.isArchived })
}

// ---- add / edit form ----

function startAdd() {
      editingId.value = null
      form.value = emptyForm()
      formError.value = ''
      formOpen.value = true
}

function startEdit(item) {
      editingId.value = item.id
      form.value = {
        name: item.name,
        description: item.description ?? '',
        price: item.price,
        categoryId: item.categoryId,
      }
      formError.value = ''
      formOpen.value = true
}

function closeForm() {
      formOpen.value = false
      editingId.value = null
      formError.value = ''
}

async function submit() {
      if (saving.value) return

      saving.value = true
      formError.value = ''
      const editing = editingId.value !== null

      try {
        const response = await authFetch(
          editing ? `/api/admin/menu-items/${editingId.value}` : '/api/admin/menu-items',
          {
            method: editing ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              name: form.value.name,
              description: form.value.description,
              price: form.value.price,
              categoryId: form.value.categoryId,
            }),
          }
        )

        if (!response.ok) {
          formError.value = await readError(response, 'Could not save the item.')
          return
        }

        const saved = await response.json()
        if (editing) replaceItem(saved)
        else items.value.push(saved)
        closeForm()
      } catch (e) {
        if (e.message !== 'Session expired') formError.value = 'Could not reach the server.'
        console.error(e)
      } finally {
        saving.value = false
      }
}
</script>

<template>
    <main>
        <StaffNav />
        <h1>Menu</h1>

        <div class="toolbar">
            <label>
                <input type="checkbox" v-model="showArchived" />
                Show archived
            </label>
            <button class="primary" @click="startAdd">Add item</button>
        </div>

        <p v-if="actionError" class="error">{{ actionError }}</p>

        <form v-if="formOpen" class="form" @submit.prevent="submit">
            <h2>{{ editingId === null ? 'Add item' : 'Edit item' }}</h2>

            <label>
                Name
                <input v-model="form.name" required maxlength="100" />
            </label>

            <label>
                Description
                <input v-model="form.description" maxlength="500" />
            </label>

            <label>
                Price
                <input v-model.number="form.price" type="number" step="0.01" min="0.01" required />
            </label>

            <label>
                Category
                <select v-model="form.categoryId" required>
                    <option :value="null" disabled>Choose a category</option>
                    <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
                </select>
            </label>

            <p v-if="formError" class="error">{{ formError }}</p>

            <div class="form-actions">
                <button type="submit" class="primary" :disabled="saving">
                    {{ saving ? 'Saving...' : 'Save' }}
                </button>
                <button type="button" @click="closeForm">Cancel</button>
            </div>
        </form>

        <p v-if="loading">Loading...</p>
        <p v-else-if="error" class="error">{{ error }}</p>
        <p v-else-if="visibleItems.length === 0">No items to show.</p>

        <div v-else class="table-wrap">
            <table>
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Category</th>
                        <th>Price</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    <tr v-for="item in visibleItems" :key="item.id" :class="{ archived: item.isArchived }">
                        <td>
                            {{ item.name }}
                            <span v-if="item.isArchived" class="badge badge-archived">Archived</span>
                            <span v-if="!item.isAvailable" class="badge badge-soldout">Sold out</span>
                        </td>
                        <td>{{ item.categoryName }}</td>
                        <td>${{ item.price.toFixed(2) }}</td>
                        <td class="row-actions">
                            <button @click="startEdit(item)">Edit</button>
                            <button v-if="!item.isArchived"
                                    :disabled="busyId !== null"
                                    @click="toggleAvailability(item)">
                                {{ item.isAvailable ? 'Mark sold out' : 'Mark available' }}
                            </button>
                            <button :disabled="busyId !== null" @click="toggleArchive(item)">
                                {{ item.isArchived ? 'Restore' : 'Archive' }}
                            </button>
                        </td>
                    </tr>
                </tbody>
            </table>
        </div>
    </main>
</template>

<style scoped>
    main {
        max-width: 900px;
        margin: 0 auto;
        padding: 1rem;
    }

    .toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 1rem;
    }

    .error {
        color: #b00020;
    }

    .form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        max-width: 420px;
        padding: 1rem;
        margin-bottom: 1.5rem;
        background: #f5f5f5;
        border-radius: 8px;
    }

        .form h2 {
            margin: 0;
        }

        .form label {
            display: flex;
            flex-direction: column;
            gap: 0.25rem;
            font-weight: 600;
        }

        .form input,
        .form select {
            padding: 0.5rem;
            font-size: 1rem;
            border: 1px solid #bbb;
            border-radius: 6px;
        }

    .form-actions,
    .row-actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
    }

    button {
        padding: 0.5rem 0.9rem;
        font-size: 0.95rem;
        border: 1px solid #bbb;
        border-radius: 8px;
        background: #fff;
        cursor: pointer;
    }

        button.primary {
            background: #1565c0;
            border-color: #1565c0;
            color: #fff;
            font-weight: 600;
        }

        button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }

    .table-wrap {
        overflow-x: auto;
    }

    table {
        width: 100%;
        border-collapse: collapse;
    }

    th,
    td {
        padding: 0.6rem 0.5rem;
        text-align: left;
        border-bottom: 1px solid #ddd;
    }

    tr.archived td:not(.row-actions) {
        opacity: 0.55;
    }

    .badge {
        margin-left: 0.4rem;
        padding: 0.1rem 0.5rem;
        border-radius: 999px;
        font-size: 0.75rem;
        font-weight: 600;
    }

    .badge-archived {
        background: #e0e0e0;
        color: #444;
    }

    .badge-soldout {
        background: #ffe0b2;
        color: #e65100;
    }
</style>