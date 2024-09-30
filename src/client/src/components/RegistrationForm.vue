<script setup lang="tsx">
import type { ReleasedSchedule, ReservationType, ScheduleEntry } from '@/DataTransfer'
import { uiFetch } from '@/UIFetch'
import { DateTime } from '@/Utils'
import _ from 'lodash'
import { marked } from 'marked'
import { computed, ref, watch } from 'vue'
import LoadButton from './LoadButton.vue'

const props = defineProps<{
  schedule: ReleasedSchedule
}>()

const dates = computed(() => _.uniq(props.schedule.entries.map(v => new Date(v.startTime).toDateString())))

const totalFreeSlots = computed(() => {
  return _.sumBy(props.schedule.entries, v => v.reservationType.type === 'free' ? v.reservationType.maxQuantity : 0)
})

const infoText = computed(() => marked.parse(props.schedule.infoText))

const selectedDate = ref<string>()
const selectedEntry = ref<ScheduleEntry>()
const selectedQuantity = ref<number>()
const contactName = ref<string>()
const contactMailAddress = ref<string>()
const contactPhoneNumber = ref<string>()

watch(selectedDate, () => {
  if (selectedEntry.value === undefined) return
  if (selectedDate.value === undefined) {
    selectedEntry.value = undefined
    return
  }
  const selectedDateTime = new Date(selectedDate.value)
  const selectedEntryTime = new Date(selectedEntry.value.startTime)

  selectedEntry.value = props.schedule.entries
    .find(v =>
      DateTime.dateEquals(selectedDateTime, new Date(v.startTime))
      && DateTime.timeEquals(selectedEntryTime, new Date(v.startTime))
    )
})
watch(selectedEntry, newSelectedEntry => {
  if (selectedQuantity.value === undefined) return
  if (newSelectedEntry === undefined) {
    selectedQuantity.value = undefined
    return
  }
  if (newSelectedEntry.reservationType.type === 'taken') {
    selectedQuantity.value = undefined
    return
  }
  if (newSelectedEntry.reservationType.type === 'free' && selectedQuantity.value > newSelectedEntry.reservationType.maxQuantity) {
    selectedQuantity.value = undefined
    return
  }
})

const getFreeSlotsAtDate = (date: Date) => {
  return _.sumBy(props.schedule.entries.filter(v => DateTime.dateEquals(date, new Date(v.startTime))), v => v.reservationType.type === 'free' ? v.reservationType.maxQuantity : 0)
}

const getFreeSlotsAtTime = (entry: ScheduleEntry) => {
  return entry.reservationType.type === 'free' ? entry.reservationType.maxQuantity : 0
}

const pluralize = (v: number, singularText: string, pluralText: string) => {
  if (v === 1) {
    return `${v} ${singularText}`
  }
  return `${v} ${pluralText}`
}

const isRegistering = ref(false)
const hasRegisteringFailed = ref(false)
const isRegistered = ref(false)
const registrationErrorMessages = ref([] as string[])
const doRegister = async () => {
  isRegistered.value = false
  if (selectedEntry.value === undefined) {
    registrationErrorMessages.value = [ 'Bitte wählen Sie Datum und Uhrzeit aus.' ]
    return
  }
  if (selectedEntry.value?.reservationType.type !== 'free') {
    registrationErrorMessages.value = [ `Zum ausgewählten Zeitpunkt sind leider keine Plätze mehr frei. Bitte kontaktieren Sie uns unter <a href="mailto:office@htlvb.at?subject=${props.schedule.title}" class="underline">office@htlvb.at</a> bzw. <a href="tel:+43767224605" class="underline">07672/24605</a>.` ]
    return
  }
  if (selectedQuantity.value === undefined) {
    registrationErrorMessages.value = [ `Bitte geben Sie die Anzahl der Personen an.` ]
    return
  }

  registrationErrorMessages.value = []

  const result = await uiFetch(isRegistering, hasRegisteringFailed, selectedEntry.value.reservationType.url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      quantity: selectedQuantity.value,
      name: contactName.value,
      mailAddress: contactMailAddress.value,
      phoneNumber: contactPhoneNumber.value,
    })
  })
  if (result.succeeded) {
    isRegistered.value = true
    selectedQuantity.value = undefined
    contactName.value = undefined
    contactMailAddress.value = undefined
    contactPhoneNumber.value = undefined
    selectedEntry.value.reservationType = await result.response.json() as ReservationType
    if (selectedEntry.value.reservationType.type === 'taken') {
      selectedEntry.value = undefined
    }
  }
  else {
    registrationErrorMessages.value = [ `Beim Speichern der Registrierung ist ein Fehler aufgetreten. Bitte versuchen sie es erneut oder kontaktieren sie uns unter <a href="mailto:office@htlvb.at?subject=${props.schedule.title}" class="underline">office@htlvb.at</a> bzw. <a href="tel:+43767224605" class="underline">07672/24605</a>.` ]
  }
}
</script>

<template>
  <div v-if="totalFreeSlots === 0 && !isRegistered && !isRegistering && registrationErrorMessages.length === 0" class="flex justify-center items-center gap-2 p-4 rounded text-white font-semibold">
    <span>
      Leider sind keine Termine mehr frei.
      Bitte kontaktieren sie uns unter
      <a :href="`mailto:office@htlvb.at?subject=${props.schedule.title}`" class="underline">office@htlvb.at</a> bzw.
      <a href="tel:+43767224605" class="underline">07672/24605</a>.
    </span>
  </div>
  <template v-else>
    <div v-html="infoText" class="my-6"></div>
    <form @submit.prevent="doRegister">
      <fieldset :disabled="isRegistering">
        <div v-if="dates.length > 1">
          <h2 class="text-lg">Datum</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <template v-for="date in dates" :key="date">
              <button v-if="getFreeSlotsAtDate(new Date(date)) > 0" type="button" class="!flex flex-col items-center button"
                :class="{ 'button-htlvb-selected': selectedDate === date }" @click="selectedDate = date">
                <span>{{ DateTime.formatDate(new Date(date), { weekday: 'short' }) }}</span>
                <span class="text-sm">{{ pluralize(getFreeSlotsAtDate(new Date(date)), 'freier Platz', 'freie Plätze') }}</span>
              </button>
              <button v-else type="button" :disabled="true" class="!flex flex-col items-center button">
                <span>{{ DateTime.formatDate(new Date(date), { weekday: 'short' }) }}</span>
                <span class="text-sm">ausgebucht</span>
              </button>
            </template>
          </div>
        </div>
        <template v-if="selectedDate !== undefined">
          <h2 class="text-lg mt-4">Uhrzeit</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <template
              v-for="entry in schedule.entries.filter(v => selectedDate !== undefined && DateTime.dateEquals(new Date(selectedDate), new Date(v.startTime)))"
              :key="entry.startTime">
              <button v-if="entry.reservationType.type === 'free'" type="button" class="!flex flex-col items-center button"
                :class="{ 'button-htlvb-selected': selectedEntry === entry }" @click="selectedEntry = entry">
                <span>{{ DateTime.formatTime(new Date(entry.startTime)) }}</span>
                <span class="text-sm">{{ pluralize(entry.reservationType.maxQuantity, 'freier Platz', 'freie Plätze') }}</span>
              </button>
              <button v-else-if="entry.reservationType.type === 'taken'" type="button" :disabled="true" class="!flex flex-col items-center button">
                <span>{{ DateTime.formatTime(new Date(entry.startTime)) }}</span>
                <span class="text-sm">ausgebucht</span>
              </button>
            </template>
          </div>
        </template>
        <template v-if="selectedEntry !== undefined">
          <h2 class="text-lg mt-4">Anzahl Personen</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <button v-for="quantity in _.range(1, getFreeSlotsAtTime(selectedEntry) + 1)" :key="quantity" type="button" class="button"
              :class="{ 'button-htlvb-selected': selectedQuantity !== undefined && selectedQuantity >= quantity }"
              @click="selectedQuantity = quantity">
              {{ quantity }}
            </button>
          </div>
        </template>
        <h2 class="text-lg mt-4">Kontaktdaten</h2>
        <label class="input mt-2">
          <span class="input-label">Name</span>
          <input type="text" v-model="contactName" required class="input-text">
        </label>
        <label class="input mt-2">
          <span class="input-label">E-Mail-Adresse</span>
          <input type="email" v-model="contactMailAddress" required class="input-text">
        </label>
        <label class="input mt-2">
          <span class="input-label">Telefonnummer</span>
          <input type="tel" v-model="contactPhoneNumber" required class="input-text">
        </label>
        <LoadButton :loading="isRegistering" class="mt-2">Registrieren</LoadButton>
        <div class="mt-2">
          <span v-if="isRegistered" class="text-green-500">
            Ihre Registrierung wurde erfolgreich gespeichert. Sie erhalten in Kürze eine Bestätigung per Mail.
          </span>
          <ul v-else-if="registrationErrorMessages.length > 0" class="text-red-500">
            <li v-for="content in registrationErrorMessages" :key="content" v-html="content"></li>
          </ul>
        </div>
      </fieldset>
    </form>
  </template>
</template>